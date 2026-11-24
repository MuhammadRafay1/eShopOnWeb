using System;
using System.Text.Json;
using Microsoft.eShopWeb.ApplicationCore.Upvest;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

internal static class UpvestHttpClient
{
    /// <summary>Name of the configured HttpClient that carries the signing handler and base address.</summary>
    public const string Name = "upvest";
}

internal static class UpvestErrors
{
    /// <summary>
    /// Builds a safe-to-log exception from an Upvest error response. It parses the
    /// <c>application/problem+json</c> body for <c>type</c>/<c>status</c>/<c>request_id</c> and a
    /// short <c>detail</c>, but deliberately does not surface request contents or personal data.
    /// A 401 indicating a missing/forbidden scope becomes <see cref="UpvestAuthorizationException"/>.
    /// </summary>
    public static UpvestApiException FromResponse(int statusCode, string? payload, string context)
    {
        string? errorType = null;
        string? requestId = null;
        string? detail = null;

        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("type", out var t)) errorType = t.GetString();
                    if (root.TryGetProperty("request_id", out var r)) requestId = r.GetString();
                    if (root.TryGetProperty("detail", out var d)) detail = d.GetString();
                }
            }
            catch (JsonException)
            {
                // Non-JSON body; ignore its contents to avoid leaking anything sensitive.
            }
        }

        string message = $"{context}: HTTP {statusCode}" +
                         (errorType is null ? string.Empty : $", type={errorType}") +
                         (requestId is null ? string.Empty : $", request_id={requestId}") +
                         (detail is null ? string.Empty : $", detail={detail}");

        bool isScopeAuthorization = statusCode == 401 &&
            (detail?.Contains("scope", StringComparison.OrdinalIgnoreCase) == true ||
             detail?.Contains("not authorised", StringComparison.OrdinalIgnoreCase) == true ||
             detail?.Contains("not authorized", StringComparison.OrdinalIgnoreCase) == true);

        return isScopeAuthorization
            ? new UpvestAuthorizationException(statusCode, errorType, requestId, message)
            : new UpvestApiException(statusCode, errorType, requestId, message);
    }
}
