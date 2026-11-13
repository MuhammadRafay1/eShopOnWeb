using System.Text.Json;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Shared constants and helpers for talking to Upvest.</summary>
public static class UpvestHttp
{
    /// <summary>Name of the configured <see cref="System.Net.Http.HttpClient"/> (BaseAddress = Upvest base URL).</summary>
    public const string ClientName = "upvest";

    /// <summary>OAuth2 scopes requested per operation group. Scoped tokens are the Upvest norm.</summary>
    public static class Scopes
    {
        // User reads require the admin scope with this client (the read scope is not granted).
        public const string Users = "users:admin";
        public const string Taxes = "taxes:admin";
        public const string Checks = "checks:admin";
        public const string Accounts = "accounts:admin";
        public const string PortfoliosAdmin = "portfolios:admin";
        public const string PortfoliosRead = "portfolios:read";
        public const string Topups = "topups:admin";
        public const string ValuationsRead = "valuations:read";
    }

    /// <summary>JSON options: snake_case on the wire (Upvest convention), tolerant on read.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Pulls the human-readable <c>detail</c> out of an Upvest problem-details body, if present.
    /// Never includes request payloads, so it is safe to surface and log.
    /// </summary>
    public static string? ExtractDetail(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON; fall through.
        }

        return payload.Length > 300 ? payload[..300] : payload;
    }
}
