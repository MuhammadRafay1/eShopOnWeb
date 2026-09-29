using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Shared plumbing for the typed PayPal clients: attaches the bearer token, retries once on 401
/// with a fresh token, and turns any non-2xx response into a structured <see cref="PayPalApiException"/>.
///
/// IMPORTANT — card data must never be logged. Request bodies that may contain a PAN / CVV
/// (number / security_code) are serialized here and sent directly to PayPal; they are never written
/// to any log. Only PayPal resource ids, statuses and debug_id (on error) may be logged, by callers.
/// </summary>
public abstract class PayPalClientBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IPayPalAccessTokenProvider _tokenProvider;

    protected PayPalClientBase(HttpClient httpClient, IPayPalAccessTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    /// <summary>
    /// Sends a request with a bearer token, retrying once with a fresh token on 401. Builds the
    /// message via <paramref name="messageFactory"/> each attempt (an HttpRequestMessage cannot be
    /// resent). Returns the parsed JSON root, or null for a 204 / empty body.
    /// </summary>
    protected async Task<JsonElement?> SendAsync(
        Func<HttpRequestMessage> messageFactory,
        CancellationToken cancellationToken)
    {
        var response = await SendOnceAsync(messageFactory, forceRefreshToken: false, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            response = await SendOnceAsync(messageFactory, forceRefreshToken: true, cancellationToken);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw ParseError((int)response.StatusCode, body);
            }

            if (response.StatusCode == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        Func<HttpRequestMessage> messageFactory, bool forceRefreshToken, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(forceRefreshToken, cancellationToken);
        var message = messageFactory();
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(message, cancellationToken);
    }

    private static PayPalApiException ParseError(int status, string body)
    {
        string? name = null, message = null, debugId = null;
        var details = new List<PayPalErrorDetail>();

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("name", out var n)) name = n.GetString();
                if (root.TryGetProperty("message", out var m)) message = m.GetString();
                if (root.TryGetProperty("debug_id", out var d)) debugId = d.GetString();
                // OAuth2 errors use error / error_description instead of name / message.
                if (name is null && root.TryGetProperty("error", out var e)) name = e.GetString();
                if (message is null && root.TryGetProperty("error_description", out var ed)) message = ed.GetString();
                if (root.TryGetProperty("details", out var det) && det.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in det.EnumerateArray())
                    {
                        details.Add(new PayPalErrorDetail
                        {
                            Field = item.TryGetProperty("field", out var f) ? f.GetString() : null,
                            Value = item.TryGetProperty("value", out var v) ? v.GetString() : null,
                            Issue = item.TryGetProperty("issue", out var i) ? i.GetString() : null,
                            Description = item.TryGetProperty("description", out var de) ? de.GetString() : null
                        });
                    }
                }
            }
            catch (JsonException)
            {
                message = "PayPal returned a non-JSON error response.";
            }
        }

        return new PayPalApiException(status, name, message, debugId, details);
    }

    // ----- small JSON extraction helpers shared by the concrete clients -----

    protected static string? GetString(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    protected static bool TryGetProperty(JsonElement el, string prop, out JsonElement value)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out value))
        {
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>Reads a { currency_code, value } money object's decimal value.</summary>
    protected static decimal? GetMoneyValue(JsonElement money)
        => TryGetProperty(money, "value", out var v) && v.ValueKind == JsonValueKind.String
            ? PayPalMoney.Parse(v.GetString())
            : null;

    protected static DateTimeOffset? GetDateTime(JsonElement el, string prop)
        => TryGetProperty(el, prop, out var v) && v.ValueKind == JsonValueKind.String
           && DateTimeOffset.TryParse(v.GetString(), out var parsed)
            ? parsed
            : null;
}
