using System;
using System.Text.Json;

namespace Microsoft.eShopWeb.Infrastructure.Portfolios;

/// <summary>
/// Builds a concise, safe description of an Upvest error response. It surfaces Upvest's own error
/// text (which aids diagnosis, e.g. the sandbox "not authorised to use these scopes" message) but
/// never echoes anything the caller sent, so no personal data is reflected.
/// </summary>
internal static class UpvestErrors
{
    public static string Describe(UpvestResponse response, string operation)
    {
        string? detail = TryExtractMessage(response.Body);
        return detail is null
            ? $"Upvest {operation} failed with HTTP {response.Status}."
            : $"Upvest {operation} failed with HTTP {response.Status}: {detail}";
    }

    private static string? TryExtractMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Trim(body);

            foreach (var name in new[] { "message", "error_description", "error", "detail", "title" })
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return Trim(text!);
                }
            }
            return null;
        }
        catch (JsonException)
        {
            return Trim(body);
        }
    }

    private static string Trim(string text)
    {
        text = text.Trim();
        return text.Length > 300 ? text[..300] : text;
    }
}
