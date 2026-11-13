using System.Collections.Generic;
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
/// Flow 2 — operator action: create or replace a model portfolio. Administrator-only.
/// </summary>
public class PutModelEndpoint : PortfolioEndpointBase, IEndpoint<IResult, PutModelRequest>
{
    public PutModelEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, PutModelRequest request) => await HandleAsync(request))
            .Produces<ModelResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(PutModelRequest request)
    {
        var modelId = RouteValue("modelId");
        var errors = Validate(modelId, request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var models = Service<IRepository<ModelPortfolio>>();
        var upvest = Service<IUpvestService>();

        var funds = request.Funds!.Select(f => new ModelFund(f.InstrumentId!.Trim(), f.Weight)).ToList();
        var fundWeights = funds.Select(f => new FundWeight(f.InstrumentId, f.Weight)).ToList();

        var existing = await models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), Ct);

        // Mirror the model as an Upvest allocation (create first time, full replace thereafter).
        var allocation = await upvest.SaveAllocationAsync(existing?.UpvestAllocationId, request.Name!.Trim(), fundWeights, Ct);

        ModelPortfolio model;
        if (existing is null)
        {
            model = new ModelPortfolio(modelId, request.Name!.Trim(), funds, allocation.AllocationId);
            await models.AddAsync(model, Ct);
        }
        else
        {
            existing.Replace(request.Name!.Trim(), funds, allocation.AllocationId);
            await models.UpdateAsync(existing, Ct);
            model = existing;
        }

        return Results.Ok(new ModelResponse(request.CorrelationId())
        {
            ModelId = model.ModelId,
            Name = model.Name,
            Funds = model.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList(),
            Detail = allocation.Determined
                ? "Model saved and mirrored to an Upvest allocation."
                : $"Model saved. {allocation.Detail}"
        });
    }

    private static Dictionary<string, string[]> Validate(string modelId, PutModelRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(modelId)) errors["modelId"] = new[] { "modelId is required." };
        if (string.IsNullOrWhiteSpace(r.Name)) errors[nameof(r.Name)] = new[] { "name is required." };
        if (r.Funds is null || r.Funds.Count == 0)
        {
            errors[nameof(r.Funds)] = new[] { "At least one fund is required." };
            return errors;
        }

        if (r.Funds.Any(f => string.IsNullOrWhiteSpace(f.InstrumentId)))
        {
            errors["Funds.InstrumentId"] = new[] { "Every fund requires an instrumentId (ISIN)." };
        }
        if (r.Funds.Any(f => f.Weight <= 0))
        {
            errors["Funds.Weight"] = new[] { "Every fund weight must be greater than zero." };
        }
        var sum = r.Funds.Sum(f => f.Weight);
        if (sum != 100m)
        {
            errors["Funds.Weight.Sum"] = new[] { $"Fund weights must add up to exactly 100 (got {sum})." };
        }
        return errors;
    }
}
