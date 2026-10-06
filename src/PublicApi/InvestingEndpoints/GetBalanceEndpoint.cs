using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Flow 5 — what the caller currently has set aside and the total invested so far.</summary>
public class GetBalanceEndpoint : IEndpoint<IResult>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IInvestingService service, CancellationToken ct) =>
            {
                var buyerId = InvestingMapping.BuyerId(user);
                if (string.IsNullOrWhiteSpace(buyerId)) return Results.Unauthorized();

                var balance = await service.GetBalanceAsync(buyerId, ct);
                return Results.Ok(new BalanceResponse
                {
                    PendingAmount = InvestingMapping.Euros(balance.PendingAmountCents),
                    InvestedAmount = InvestingMapping.Euros(balance.InvestedAmountCents),
                });
            })
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public Task<IResult> HandleAsync() => Task.FromResult<IResult>(Results.Empty);
}
