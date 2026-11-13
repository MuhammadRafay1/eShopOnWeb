using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 2 — the signed-in investor chooses which model their portfolio follows.
/// </summary>
public class ChooseModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ChooseModelRequest request, ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var shopperId = CallerIdentity.GetShopperId(user);
                if (string.IsNullOrEmpty(shopperId))
                {
                    return Results.Unauthorized();
                }

                var result = await service.ChooseModelAsync(shopperId, request.ModelId, ct);
                return result.Status switch
                {
                    PortfolioOperationStatus.Ok => Results.Ok(new
                    {
                        investorId = result.Value!.InvestorId,
                        status = result.Value.Status,
                        modelId = result.Value.ModelId
                    }),
                    PortfolioOperationStatus.NotFound => Results.NotFound(new { message = result.Message }),
                    PortfolioOperationStatus.NotEligible => Results.Conflict(new { message = result.Message }),
                    _ => Results.BadRequest(new { message = result.Message })
                };
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }
}
