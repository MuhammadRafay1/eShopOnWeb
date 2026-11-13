using System;
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
/// GET /api/portfolio/realignments/{realignmentId} — where that realignment has got to (status reflects Upvest).
/// </summary>
public class GetRealignmentEndpoint : IEndpoint<IResult, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:guid}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (Guid realignmentId, IPortfolioService service, CancellationToken ct) =>
            {
                var realignment = await service.GetRealignmentAsync(realignmentId, ct);
                if (realignment is null) return Results.NotFound();

                return Results.Ok(new RealignmentResponse
                {
                    RealignmentId = realignment.RealignmentId,
                    Status = StatusText.For(realignment.Status),
                });
            })
            .Produces<RealignmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    // Logic is in the route lambda (it needs the route value and cancellation token).
    public Task<IResult> HandleAsync(IPortfolioService service) => Task.FromResult(Results.BadRequest() as IResult);
}
