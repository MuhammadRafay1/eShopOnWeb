using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Investing.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Webhooks;

public enum WebhookVerification { NotSigned, Verified, Failed }

/// <summary>Everything the webhook endpoint needs to hand over, free of ASP.NET types.</summary>
public record UpvestWebhookRequest(
    string Method,
    string Path,
    string Query,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body);

public interface IUpvestWebhookService
{
    Task<int> HandleAsync(UpvestWebhookRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Parses an inbound Upvest webhook batch and turns each event into a targeted reconciliation.
/// A webhook is only ever treated as a hint that "something changed" — the authoritative state is
/// always re-read from Upvest through the authenticated client, so a forged webhook cannot inject
/// false state. Signatures are still verified for observability; the result is logged, not trusted
/// as a gate (the re-read is the real protection).
/// </summary>
public sealed class UpvestWebhookService : IUpvestWebhookService
{
    private static readonly ConcurrentDictionary<string, ECParameters> KeyCache = new();

    private readonly IInvestingProcessor _processor;
    private readonly IUpvestInvestmentClient _upvest;
    private readonly ILogger<UpvestWebhookService> _logger;

    public UpvestWebhookService(
        IInvestingProcessor processor,
        IUpvestInvestmentClient upvest,
        ILogger<UpvestWebhookService> logger)
    {
        _processor = processor;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<int> HandleAsync(UpvestWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var verification = await VerifyAsync(request, cancellationToken);
        _logger.LogInformation("Upvest webhook received (signature {Verification}).", verification);

        using var doc = JsonDocument.Parse(request.Body);
        if (!doc.RootElement.TryGetProperty("payload", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var handled = 0;
        foreach (var evt in events.EnumerateArray())
        {
            var type = evt.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var obj = evt.TryGetProperty("object", out var o) ? o : default;
            await DispatchAsync(type, obj, cancellationToken);
            handled++;
        }
        return handled;
    }

    private async Task DispatchAsync(string type, JsonElement obj, CancellationToken cancellationToken)
    {
        if (type.StartsWith("ORDER", StringComparison.OrdinalIgnoreCase) ||
            type.StartsWith("EXECUTION", StringComparison.OrdinalIgnoreCase))
        {
            var orderId = GetString(obj, "order_id") ?? GetString(obj, "id");
            if (!string.IsNullOrEmpty(orderId))
            {
                await _processor.ReconcileInvestmentByUpvestOrderAsync(orderId!, cancellationToken);
            }
            else
            {
                await _processor.ReconcilePendingInvestmentsAsync(cancellationToken);
            }
            return;
        }

        if (type.StartsWith("USER.", StringComparison.OrdinalIgnoreCase))
        {
            var userId = GetString(obj, "id");
            if (!string.IsNullOrEmpty(userId))
            {
                await _processor.ReconcileEnrolmentByUpvestUserAsync(userId!, cancellationToken);
                return;
            }
        }

        if (type.StartsWith("USER", StringComparison.OrdinalIgnoreCase) ||
            type.StartsWith("ACCOUNT", StringComparison.OrdinalIgnoreCase))
        {
            // USER_CHECK / ACCOUNT / ACCOUNT_GROUP events: reconcile all pending enrolments.
            await _processor.ReconcilePendingEnrolmentsAsync(cancellationToken);
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private async Task<WebhookVerification> VerifyAsync(UpvestWebhookRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetHeader(request.Headers, "signature", out var signatureHeader) ||
            !TryGetHeader(request.Headers, "signature-input", out var signatureInput))
        {
            return WebhookVerification.NotSigned;
        }

        try
        {
            // signature-input: sig1=("c1" "c2" ...);keyid="...";created=...;nonce="..."
            var eq = signatureInput.IndexOf('=');
            var paramsString = signatureInput[(eq + 1)..].Trim();
            var componentList = paramsString[(paramsString.IndexOf('(') + 1)..paramsString.IndexOf(')')];
            var components = componentList.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim('"'))
                .ToArray();
            var keyId = ExtractParam(paramsString, "keyid");

            var baseBuilder = new StringBuilder();
            foreach (var component in components)
            {
                var value = component switch
                {
                    "@method" => request.Method.ToUpperInvariant(),
                    "@path" => request.Path,
                    "@query" => string.IsNullOrEmpty(request.Query) ? "?" : request.Query,
                    _ => TryGetHeader(request.Headers, component, out var hv) ? hv : null
                };
                if (value is null)
                {
                    return WebhookVerification.Failed;
                }
                baseBuilder.Append('"').Append(component).Append("\": ").Append(value).Append('\n');
            }
            baseBuilder.Append("\"@signature-params\": ").Append(paramsString);

            var signatureBytes = ParseSignature(signatureHeader);
            if (signatureBytes is null || keyId is null)
            {
                return WebhookVerification.Failed;
            }

            var key = await GetKeyAsync(keyId, cancellationToken);
            if (key is null)
            {
                return WebhookVerification.Failed;
            }

            using var ecdsa = ECDsa.Create(key.Value);
            var ok = ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(baseBuilder.ToString()),
                signatureBytes,
                HashAlgorithmName.SHA512,
                DSASignatureFormat.Rfc3279DerSequence);
            return ok ? WebhookVerification.Verified : WebhookVerification.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Upvest webhook signature verification raised an error.");
            return WebhookVerification.Failed;
        }
    }

    private async Task<ECParameters?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        if (KeyCache.TryGetValue(keyId, out var cached))
        {
            return cached;
        }

        var keys = await _upvest.GetVerifyKeysAsync(cancellationToken);
        foreach (var k in keys.Where(k => k.Crv.Contains("521", StringComparison.Ordinal)))
        {
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP521,
                Q = new ECPoint { X = Base64Url.Decode(k.X), Y = Base64Url.Decode(k.Y) }
            };
            KeyCache[k.Kid] = parameters;
        }

        return KeyCache.TryGetValue(keyId, out var found) ? found : null;
    }

    private static string? ExtractParam(string paramsString, string name)
    {
        var marker = name + "=\"";
        var start = paramsString.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }
        start += marker.Length;
        var end = paramsString.IndexOf('"', start);
        return end < 0 ? null : paramsString[start..end];
    }

    private static byte[]? ParseSignature(string signatureHeader)
    {
        // sig1=:BASE64:
        var first = signatureHeader.IndexOf(':');
        var last = signatureHeader.LastIndexOf(':');
        if (first < 0 || last <= first)
        {
            return null;
        }
        var b64 = signatureHeader[(first + 1)..last];
        return Convert.FromBase64String(b64);
    }

    private static bool TryGetHeader(IReadOnlyDictionary<string, string> headers, string name, out string value)
    {
        foreach (var pair in headers)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }
}
