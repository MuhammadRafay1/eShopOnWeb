using System.Net;
using System.Threading;

namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

/// <summary>
/// Carries the HTTP status of the most recent PayPal response on the current async flow. It lets the gateway
/// boundary recover the real status when the SDK throws a <see cref="System.Text.Json.JsonException"/> while
/// constructing a typed error object (which otherwise destroys the status), so a deterministic 4xx rejection
/// is not misreported as a 5xx outage that a caller would keep retrying.
/// </summary>
public static class PayPalRequestContext
{
    private static readonly AsyncLocal<HttpStatusCode?> _lastStatusCode = new();

    public static HttpStatusCode? LastStatusCode
    {
        get => _lastStatusCode.Value;
        set => _lastStatusCode.Value = value;
    }
}
