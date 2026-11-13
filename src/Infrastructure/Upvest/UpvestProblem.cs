using System.Text.Json;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Extracts a short, non-sensitive summary from an Upvest <c>application/problem+json</c> error body.
/// Upvest error bodies carry a type/title/detail but no personal data.
/// </summary>
public static class UpvestProblem
{
    public static string Summarize(string? body, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"HTTP {statusCode}";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            string? Get(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var detail = Get("detail") ?? Get("error_description");
            var title = Get("title") ?? Get("type") ?? Get("error");

            if (!string.IsNullOrWhiteSpace(detail))
            {
                return $"HTTP {statusCode}: {detail}";
            }
            if (!string.IsNullOrWhiteSpace(title))
            {
                return $"HTTP {statusCode}: {title}";
            }
        }
        catch (JsonException)
        {
            // Not JSON; fall through to the status-only summary (avoid echoing arbitrary bodies).
        }

        return $"HTTP {statusCode}";
    }
}
