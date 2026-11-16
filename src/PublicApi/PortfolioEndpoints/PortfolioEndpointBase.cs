using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Shared dependencies for the portfolio endpoints. Caller identity and the request cancellation token come
/// from the ambient <see cref="IHttpContextAccessor"/>, so each endpoint's <c>HandleAsync</c> matches the
/// <c>MinimalApi.Endpoint</c> interface shape while still resolving the signed-in shopper.
/// </summary>
public abstract class PortfolioEndpointBase
{
    protected PortfolioEndpointBase(
        IUpvestPortfolioGateway gateway,
        IPortfolioStore store,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger)
    {
        Gateway = gateway;
        Store = store;
        HttpContextAccessor = httpContextAccessor;
        Logger = logger;
    }

    protected IUpvestPortfolioGateway Gateway { get; }
    protected IPortfolioStore Store { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }
    protected ILogger Logger { get; }

    protected HttpContext Http => HttpContextAccessor.HttpContext!;
    protected CancellationToken Ct => Http.RequestAborted;
    protected string? BuyerId => PortfolioShared.GetBuyerId(Http.User);
}
