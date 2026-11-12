using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// GET /api/portfolio — the caller's portfolio as Upvest currently records it, plus the total amount paid in.
/// </summary>
public class GetPortfolioEndpoint : IEndpoint<IResult, HttpContext>
{
    private readonly IUpvestInvestmentService _service;
    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Payment> _payments;
    private readonly IAppLogger<GetPortfolioEndpoint> _logger;

    public GetPortfolioEndpoint(IUpvestInvestmentService service, IRepository<Investor> investors, IRepository<Payment> payments, IAppLogger<GetPortfolioEndpoint> logger)
    {
        _service = service;
        _investors = investors;
        _payments = payments;
        _logger = logger;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(http, ct);
            })
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(HttpContext http) => HandleAsync(http, default);

    public async Task<IResult> HandleAsync(HttpContext http, CancellationToken ct)
    {
        var buyerId = PortfolioEndpointHelpers.GetBuyerId(http);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investor = await _investors.GetByIdAsync(buyerId, ct);
        if (investor is null || investor.UpvestUserId is null)
        {
            return Results.NotFound(new { message = "You are not signed up as an investor." });
        }

        // The total amount paid in is the sum of the shopper's accepted payments (everything not failed).
        var payments = await _payments.ListAsync(new PaymentsByBuyerSpecification(buyerId), ct);
        var paidIn = PortfolioEndpointHelpers.Money(payments.Where(p => p.Status != PaymentStatus.Failed).Sum(p => p.Amount));

        var response = new PortfolioResponse
        {
            ModelId = investor.FollowedModelId,
            Holdings = new List<HoldingDto>(),
            TotalValue = 0m,
            PaidIn = paidIn
        };

        // Read what Upvest records for the account, when one is provisioned. Degrade to the app view if the
        // valuation scope is refused (sandbox).
        if (investor.AccountId is { } accountId)
        {
            try
            {
                var portfolio = await _service.GetPortfolioAsync(accountId, ct);
                response.Holdings = portfolio.Holdings
                    .Select(h => new HoldingDto
                    {
                        InstrumentId = h.InstrumentId,
                        Quantity = h.Quantity,
                        Value = PortfolioEndpointHelpers.Money(h.Value)
                    }).ToList();
                response.TotalValue = PortfolioEndpointHelpers.Money(portfolio.TotalValue);
            }
            catch (UpvestIntegrationException ex) when (ex.Kind == UpvestFailureKind.ProviderRejectedUs)
            {
                _logger.LogWarning("Portfolio valuation not read from Upvest (status {Status}): client not authorised. Returning the app view.", ex.StatusCode ?? 0);
            }
        }

        return Results.Ok(response);
    }
}
