using System.Linq;
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

/// <summary>Flow 3 — the caller's investments, newest first.</summary>
public class ListInvestmentsEndpoint : IEndpoint<IResult>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IInvestingService service, CancellationToken ct) =>
            {
                var buyerId = InvestingMapping.BuyerId(user);
                if (string.IsNullOrWhiteSpace(buyerId)) return Results.Unauthorized();

                var investments = await service.GetInvestmentsAsync(buyerId, ct);
                var payload = investments.Select(i => new InvestmentResponse
                {
                    InvestmentId = i.InvestmentId,
                    Amount = InvestingMapping.Euros(i.AmountCents),
                    Status = InvestingMapping.ToWire(i.Status),
                }).ToList();

                return Results.Ok(payload);
            })
            .Produces<System.Collections.Generic.List<InvestmentResponse>>()
            .WithTags("InvestingEndpoints");
    }

    public Task<IResult> HandleAsync() => Task.FromResult<IResult>(Results.Empty);
}
