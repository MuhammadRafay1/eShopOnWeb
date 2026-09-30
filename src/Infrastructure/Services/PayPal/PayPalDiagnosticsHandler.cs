using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

/// <summary>
/// Records the status code of each PayPal HTTP response into <see cref="PayPalRequestContext"/> before the
/// SDK deserializes it, and logs verb/path/status at information level. This is the seam the gateway relies
/// on to recover a real HTTP status if the SDK later throws while parsing the response body.
/// </summary>
public sealed class PayPalDiagnosticsHandler : DelegatingHandler
{
    private readonly IAppLogger<PayPalDiagnosticsHandler> _logger;

    public PayPalDiagnosticsHandler(IAppLogger<PayPalDiagnosticsHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        PayPalRequestContext.LastStatusCode = response.StatusCode;
        _logger.LogInformation("PayPal {0} {1} -> {2}",
            request.Method, request.RequestUri?.AbsolutePath, (int)response.StatusCode);
        return response;
    }
}
