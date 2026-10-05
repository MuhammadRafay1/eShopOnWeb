using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Raised when Upvest returns a non-success status. Carries the HTTP status and Upvest's generic
/// problem+json detail (which never contains personal data) for diagnostics.
/// </summary>
public class UpvestApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public UpvestApiException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public static async Task<UpvestApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string detail;
        try
        {
            detail = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            detail = string.Empty;
        }

        var method = response.RequestMessage?.Method;
        var path = response.RequestMessage?.RequestUri?.AbsolutePath;
        return new UpvestApiException(
            response.StatusCode,
            $"Upvest {method} {path} returned {(int)response.StatusCode}. {detail}".Trim());
    }
}
