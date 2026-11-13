using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 4 — reports where a realignment has got to, reflecting the execution's outcome at Upvest.
/// </summary>
public class GetRealignmentEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public GetRealignmentEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:int}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int realignmentId) => await HandleAsync())
            .Produces<RealignmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        if (!int.TryParse(RouteValue("realignmentId"), out var realignmentId))
        {
            return Results.NotFound(new { detail = "Unknown realignment." });
        }

        var realignments = Service<IRepository<Realignment>>();
        var upvest = Service<IUpvestService>();

        var realignment = await realignments.GetByIdAsync(realignmentId, Ct);
        if (realignment is null)
        {
            return Results.NotFound(new { detail = $"No realignment {realignmentId} exists." });
        }

        string? detail = realignment.StatusDetail;
        if (realignment.Status == RealignmentStatus.Pending && realignment.UpvestExecutionId is not null)
        {
            var refreshed = await upvest.GetRealignmentStatusAsync(realignment.UpvestExecutionId, Ct);
            if (refreshed.Determined && refreshed.Status != realignment.Status)
            {
                realignment.SetStatus(refreshed.Status, refreshed.Detail);
                await realignments.UpdateAsync(realignment, Ct);
                detail = refreshed.Detail;
            }
        }

        return Results.Ok(new RealignmentResponse(System.Guid.NewGuid())
        {
            RealignmentId = realignment.Id,
            Status = realignment.Status.ToString().ToLowerInvariant(),
            Detail = detail
        });
    }
}
