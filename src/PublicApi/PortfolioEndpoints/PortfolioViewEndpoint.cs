using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 5 — the caller's portfolio as Upvest currently records it.</summary>
public sealed class GetPortfolioEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public GetPortfolioEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<GetPortfolioEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async () => await HandleAsync())
            .Produces<PortfolioResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        var buyerId = BuyerId;
        if (buyerId is null)
        {
            return Results.Unauthorized();
        }

        var investor = Store.GetInvestor(buyerId);
        if (investor is null || investor.UpvestUserId is null)
        {
            return Results.NotFound(new { error = "You are not registered as an investor." });
        }

        if (investor.AccountId is not { } accountId)
        {
            return Results.Conflict(new { error = "Your investor account is not ready yet." });
        }

        // The total paid in is the sum of this shopper's own recorded pay-ins (Upvest exposes no single figure).
        var paidIn = Store.ListPayments(buyerId).Sum(p => p.Amount);

        try
        {
            var snapshot = await Gateway.GetPortfolioAsync(accountId, Ct);
            return Results.Ok(new PortfolioResponse
            {
                ModelId = investor.SelectedModelId,
                Holdings = snapshot.Holdings
                    .Select(h => new HoldingDto { InstrumentId = h.InstrumentId, Quantity = h.Quantity, Value = h.Value })
                    .ToList(),
                TotalValue = snapshot.TotalValue,
                PaidIn = paidIn,
            });
        }
        catch (UpvestGatewayException ex)
        {
            Logger.LogWarning("Portfolio read failed for a shopper (status {Status}).", ex.StatusCode);
            return PortfolioShared.ToProblem(ex);
        }
    }
}
