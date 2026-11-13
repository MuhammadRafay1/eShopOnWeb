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
/// Flow 4 — operator action: realign every portfolio that follows a model back to its current weights.
/// Administrator-only.
/// </summary>
public class CreateRealignmentEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public CreateRealignmentEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId) => await HandleAsync())
            .Produces<RealignmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        var modelId = RouteValue("modelId");
        var realignments = Service<IRepository<Realignment>>();
        var models = Service<IReadRepository<ModelPortfolio>>();
        var upvest = Service<IUpvestService>();

        var model = await models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), Ct);
        if (model is null)
        {
            return Results.NotFound(new { detail = $"No model portfolio '{modelId}' exists." });
        }

        var realignment = new Realignment(model.ModelId);
        await realignments.AddAsync(realignment, Ct);

        string? detail;
        if (model.UpvestAllocationId is not null)
        {
            var result = await upvest.TriggerRealignmentAsync(model.UpvestAllocationId, Ct);
            if (result.ExecutionId is not null) realignment.LinkExecution(result.ExecutionId);
            realignment.SetStatus(result.Status, result.Detail);
            detail = result.Detail ?? "Realignment triggered at Upvest.";
        }
        else
        {
            realignment.SetStatus(RealignmentStatus.Pending, "The model has no Upvest allocation yet, so no realignment could be triggered.");
            detail = realignment.StatusDetail;
        }
        await realignments.UpdateAsync(realignment, Ct);

        return Results.Ok(new RealignmentResponse(System.Guid.NewGuid())
        {
            RealignmentId = realignment.Id,
            Status = realignment.Status.ToString().ToLowerInvariant(),
            Detail = detail
        });
    }
}
