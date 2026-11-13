using System.Collections.Generic;
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
/// Flow 2 — the signed-in investor chooses (or switches) which model their portfolio follows.
/// </summary>
public class SelectModelEndpoint : PortfolioEndpointBase, IEndpoint<IResult, SelectModelRequest>
{
    public SelectModelEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SelectModelRequest request) => await HandleAsync(request))
            .Produces<SelectModelResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(SelectModelRequest request)
    {
        var shopperId = ShopperId;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ModelId)] = new[] { "modelId is required." }
            });
        }

        var investors = Service<IRepository<Investor>>();
        var models = Service<IReadRepository<ModelPortfolio>>();
        var upvest = Service<IUpvestService>();

        var investor = await investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), Ct);
        if (investor is null)
        {
            return Results.NotFound(new { detail = "You are not registered as an investor yet." });
        }

        var model = await models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(request.ModelId!), Ct);
        if (model is null)
        {
            return Results.NotFound(new { detail = $"No model portfolio '{request.ModelId}' exists." });
        }

        var alreadyConfigured = investor.SelectedModelId is not null;
        investor.SelectModel(model.ModelId);
        await investors.UpdateAsync(investor, Ct);

        string? detail;
        if (investor.UpvestAccountId is not null && model.UpvestAllocationId is not null)
        {
            var configured = await upvest.ConfigurePortfolioAsync(investor.UpvestAccountId, model.UpvestAllocationId, alreadyConfigured, Ct);
            detail = configured.Determined ? "Model selected and applied to your Upvest portfolio." : $"Model selected. {configured.Detail}";
        }
        else
        {
            detail = "Model selected. It will be applied to your Upvest portfolio once your account is ready.";
        }

        return Results.Ok(new SelectModelResponse(request.CorrelationId())
        {
            ModelId = model.ModelId,
            Detail = detail
        });
    }
}
