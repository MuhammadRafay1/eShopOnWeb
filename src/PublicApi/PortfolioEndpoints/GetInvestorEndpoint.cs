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
/// Flow 1 — reports where the caller's investor sign-up has got to, reflecting the state at Upvest.
/// </summary>
public class GetInvestorEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public GetInvestorEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            () => await HandleAsync())
            .Produces<InvestorResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        var shopperId = ShopperId;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var investors = Service<IRepository<Investor>>();
        var upvest = Service<IUpvestService>();

        var investor = await investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), Ct);
        if (investor is null)
        {
            return Results.NotFound(new { detail = "You are not registered as an investor yet." });
        }

        // Re-read the current acceptance state from Upvest; persist any change so it is reflected.
        var status = await upvest.GetInvestorStatusAsync(investor.UpvestUserId, investor.UpvestAccountId, Ct);
        string? detail = investor.StatusDetail;
        if (status.Determined && status.Status != investor.Status)
        {
            investor.SetStatus(status.Status, status.Detail);
            await investors.UpdateAsync(investor, Ct);
            detail = status.Detail;
        }

        return Results.Ok(CreateInvestorEndpoint.ToResponse(investor, System.Guid.NewGuid(), detail));
    }
}
