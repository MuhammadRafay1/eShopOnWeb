using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public interface IUpvestWebhookVerifier
{
    /// <summary>
    /// Verifies the HTTP Message Signature on an incoming Upvest webhook (RFC 9421), using
    /// Upvest's public verification key. Returns false if the signature is missing or invalid.
    /// </summary>
    Task<bool> VerifyAsync(string method, string path, string query, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default);
}

/// <summary>
/// Verifies webhook signatures against the public keys published at Upvest's <c>/auth/verify_keys</c>
/// (JWKS). Keys are fetched on demand and cached by key id.
/// </summary>
public sealed class UpvestWebhookVerifier : IUpvestWebhookVerifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, ECDsa> _keys = new();

    public UpvestWebhookVerifier(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public async Task<bool> VerifyAsync(string method, string path, string query, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (!headers.TryGetValue("signature", out var signatureHeader) ||
            !headers.TryGetValue("signature-input", out var signatureInput))
        {
            return false;
        }

        var paramsValue = StripLabel(signatureInput);     // ("a" "b" ...);keyid="..";created=..;...
        var signatureValue = StripLabel(signatureHeader); // :base64:
        if (paramsValue is null || signatureValue is null) return false;

        var componentNames = ParseComponentList(paramsValue);
        if (componentNames.Count == 0) return false;

        var keyId = ParseMetadata(paramsValue, "keyid");
        if (keyId is null) return false;

        var key = await GetKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
        if (key is null) return false;

        var lines = new List<string>();
        foreach (var name in componentNames)
        {
            string value;
            switch (name)
            {
                case "@method": value = method.ToUpperInvariant(); break;
                case "@path": value = path; break;
                case "@query": value = query; break;
                default:
                    if (!headers.TryGetValue(name, out var headerValue)) return false;
                    value = headerValue;
                    break;
            }
            lines.Add($"\"{name}\": {value}");
        }
        lines.Add($"\"@signature-params\": {paramsValue}");
        var signatureBase = string.Join("\n", lines);

        var signatureBytes = DecodeSignature(signatureValue);
        if (signatureBytes is null) return false;

        try
        {
            return key.VerifyData(
                Encoding.UTF8.GetBytes(signatureBase),
                signatureBytes,
                HashAlgorithmName.SHA512,
                DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private async Task<ECDsa?> GetKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        if (_keys.TryGetValue(keyId, out var cached)) return cached;

        var client = _httpClientFactory.CreateClient(UpvestHttpClient.Name);
        var jwks = await client.GetFromJsonAsync<Jwks>("auth/verify_keys", cancellationToken).ConfigureAwait(false);
        var match = jwks?.Keys?.FirstOrDefault(k => k.Kid == keyId);
        if (match is null || match.X is null || match.Y is null) return null;

        var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP521,
            Q = new ECPoint
            {
                X = Base64Url.Decode(match.X),
                Y = Base64Url.Decode(match.Y),
            },
        });
        _keys[keyId] = ecdsa;
        return ecdsa;
    }

    private static string? StripLabel(string value)
    {
        var idx = value.IndexOf('=');
        return idx < 0 ? null : value[(idx + 1)..].Trim();
    }

    private static List<string> ParseComponentList(string paramsValue)
    {
        var open = paramsValue.IndexOf('(');
        var close = paramsValue.IndexOf(')');
        if (open < 0 || close < 0 || close <= open) return new List<string>();
        var inner = paramsValue[(open + 1)..close];
        return Regex.Matches(inner, "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
    }

    private static string? ParseMetadata(string paramsValue, string name)
    {
        var match = Regex.Match(paramsValue, name + "=\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static byte[]? DecodeSignature(string signatureValue)
    {
        var trimmed = signatureValue.Trim().Trim(':');
        try { return Convert.FromBase64String(trimmed); }
        catch (FormatException) { return null; }
    }

    private sealed class Jwks
    {
        [JsonPropertyName("keys")] public List<JwkKey>? Keys { get; set; }
    }

    private sealed class JwkKey
    {
        [JsonPropertyName("kid")] public string? Kid { get; set; }
        [JsonPropertyName("x")] public string? X { get; set; }
        [JsonPropertyName("y")] public string? Y { get; set; }
    }
}

internal static class Base64Url
{
    public static byte[] Decode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
