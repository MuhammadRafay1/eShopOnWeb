using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 2 (operator) — create or replace a model portfolio.
/// PUT /api/portfolio/models/{modelId}
/// </summary>
public class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, UpsertModelRequest request, IPortfolioService service) =>
            {
                request.ModelId = modelId;
                return await HandleAsync(request, service);
            })
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(UpsertModelRequest request, IPortfolioService service)
    {
        var funds = (request.Funds ?? new List<FundRequest>())
            .Select(f => new FundWeight(f.InstrumentId, f.Weight))
            .ToList();

        var view = await service.UpsertModelAsync(request.ModelId, request.Name, funds, default);
        return Results.Ok(new ModelResponse(
            view.ModelId,
            view.Name,
            view.Funds.Select(f => new FundResponse(f.InstrumentId, f.Weight)).ToList()));
    }
}
