using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Turns a non-success Upvest response into an <see cref="UpvestApiException"/>.</summary>
internal static class UpvestError
{
    public static async Task<UpvestApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        string? type = null;
        string? detail = null;

        try
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("type", out var t)) type = t.GetString();
                    if (root.TryGetProperty("detail", out var d)) detail = d.GetString();
                    if (detail is null && root.TryGetProperty("title", out var ti)) detail = ti.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body; status alone is enough.
        }

        var message = $"Upvest returned HTTP {status}" +
            (type is not null ? $" ({type})" : string.Empty) +
            (detail is not null ? $": {detail}" : string.Empty);

        return new UpvestApiException(status, type, message);
    }
}
