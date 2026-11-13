using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.PortfolioEndpoints;

/// <summary>PUT /api/portfolio/models/{modelId} — operator creates or replaces a model portfolio (admin only).</summary>
public class PutModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, PutModelRequest request, ClaimsPrincipal user, ModelPortfolioService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async _ =>
                {
                    var funds = (request.Funds ?? new List<FundDto>())
                        .Select(f => new ModelFundInput(f.InstrumentId, f.Weight)).ToList();
                    var result = await service.PutModelAsync(modelId, request.Name, funds, ct);
                    return Results.Ok(ToResponse(result));
                }))
            .Produces<ModelResponse>()
            .WithTags(EndpointSupport.Tag);
    }

    internal static ModelResponse ToResponse(ModelPortfolioResult r) => new()
    {
        ModelId = r.ModelId,
        Name = r.Name,
        Funds = r.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList(),
    };
}

/// <summary>GET /api/portfolio/models — every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, ModelPortfolioService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async _ =>
                {
                    var models = await service.ListModelsAsync(ct);
                    return Results.Ok(new ModelsResponse { Models = models.Select(PutModelEndpoint.ToResponse).ToList() });
                }))
            .Produces<ModelsResponse>()
            .WithTags(EndpointSupport.Tag);
    }
}
