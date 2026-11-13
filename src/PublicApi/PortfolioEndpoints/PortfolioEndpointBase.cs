using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Shared plumbing for the portfolio endpoints. Endpoints are registered as scoped but their
/// <c>AddRoute</c> runs once at start-up, so per-request services must be resolved from the current
/// request scope rather than captured in the constructor. The only injected dependency is the
/// (singleton) <see cref="IHttpContextAccessor"/>, from which the request scope, caller identity,
/// route values and cancellation token are read.
/// </summary>
public abstract class PortfolioEndpointBase
{
    protected IHttpContextAccessor Accessor { get; }

    protected PortfolioEndpointBase(IHttpContextAccessor accessor) => Accessor = accessor;

    protected HttpContext Http => Accessor.HttpContext!;
    protected CancellationToken Ct => Http.RequestAborted;
    protected string? ShopperId => PortfolioCaller.GetShopperId(Http.User);
    protected T Service<T>() where T : notnull => Http.RequestServices.GetRequiredService<T>();
    protected string RouteValue(string key) => Http.Request.RouteValues[key]?.ToString() ?? string.Empty;
}
