using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 5 — the caller's portfolio as Upvest currently records it: model, holdings, current value, and amount paid in.
/// </summary>
public class GetPortfolioEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public GetPortfolioEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            () => await HandleAsync())
            .Produces<PortfolioResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        var shopperId = ShopperId;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var investors = Service<IReadRepository<Investor>>();
        var payments = Service<IReadRepository<Payment>>();
        var upvest = Service<IUpvestService>();

        var investor = await investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), Ct);
        if (investor is null)
        {
            return Results.NotFound(new { detail = "You are not registered as an investor yet." });
        }

        // Total amount paid in = everything the shopper has put in that did not fail.
        var shopperPayments = await payments.ListAsync(new PaymentsByShopperSpecification(shopperId), Ct);
        var paidIn = shopperPayments.Where(p => p.Status != PaymentStatus.Failed).Sum(p => p.Amount);

        var response = new PortfolioResponse(System.Guid.NewGuid())
        {
            ModelId = investor.SelectedModelId,
            PaidIn = paidIn
        };

        if (investor.UpvestAccountId is not null)
        {
            var portfolio = await upvest.GetPortfolioAsync(investor.UpvestAccountId, Ct);
            if (portfolio.Determined)
            {
                response.Holdings = portfolio.Holdings
                    .Select(h => new HoldingDto { InstrumentId = h.InstrumentId, Quantity = h.Quantity, Value = h.Value })
                    .ToList();
                response.TotalValue = portfolio.TotalValue;
            }
            else
            {
                response.Detail = portfolio.Detail;
            }
        }
        else
        {
            response.Detail = "Your Upvest account is not ready yet, so no holdings are available.";
        }

        return Results.Ok(response);
    }
}
