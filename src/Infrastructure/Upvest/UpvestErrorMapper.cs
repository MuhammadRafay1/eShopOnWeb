using System;
using System.Text.Json;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Turns an Upvest error response (<c>application/problem+json</c>) into a
/// <see cref="UpvestApiException"/>, preserving the error <c>type</c> and a human-readable detail
/// while never surfacing any request personal data.
/// </summary>
internal static class UpvestErrorMapper
{
    public static UpvestApiException ToException(int statusCode, string? responseBody)
    {
        string? errorType = null;
        string detail = $"Upvest returned HTTP {statusCode}.";
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String)
                    errorType = t.GetString();
                if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(d.GetString()))
                    detail = d.GetString()!;
                else if (root.TryGetProperty("title", out var ti) && ti.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(ti.GetString()))
                    detail = ti.GetString()!;
            }
            catch (JsonException)
            {
                // Non-JSON body — keep the generic detail; do not echo arbitrary content.
            }
        }
        return new UpvestApiException(statusCode, errorType, detail);
    }
}
