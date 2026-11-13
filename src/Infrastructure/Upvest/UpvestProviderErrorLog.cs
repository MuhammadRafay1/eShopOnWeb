using System;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Logs the correlation id of a failed Upvest response so provider-side failures can be traced, without
/// echoing the free-text <c>detail</c> (which can carry submitted personal data). Reads the buffered error
/// body only; success responses are never touched.
/// </summary>
internal static class UpvestProviderErrorLog
{
    public static void Record(ILogger logger, HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (status < 400) return;

        string? requestId = null;
        string? title = null;
        try
        {
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("request_id", out var rid)) requestId = rid.GetString();
                if (root.TryGetProperty("title", out var t)) title = t.GetString();
            }
        }
        catch
        {
            // Best-effort logging only — never let an observability read affect the call.
        }

        logger.LogWarning("Upvest {Method} {Path} responded {Status} (request_id {RequestId}, title {Title}).",
            response.RequestMessage?.Method, response.RequestMessage?.RequestUri?.AbsolutePath, status, requestId ?? "-", title ?? "-");
    }
}
