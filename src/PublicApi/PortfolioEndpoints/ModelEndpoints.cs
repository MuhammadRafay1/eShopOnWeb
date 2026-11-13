using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>PUT /api/portfolio/models/{modelId} — create or replace a model portfolio (operator only).</summary>
public class UpsertModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId, UpsertModelRequest request, IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var input = new ModelPortfolioInput(
                    request.Name,
                    (request.Funds ?? new List<ModelFundRequest>()).Select(f => new FundInput(f.InstrumentId, f.Weight)).ToList());

                var result = await svc.UpsertModelAsync(modelId, input, ct);
                return Results.Ok(ToResponse(result));
            })
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    internal static ModelResponse ToResponse(ModelPortfolioResult r) =>
        new(r.ModelId, r.Name, r.Funds.Select(f => new FundResponse(f.InstrumentId, f.Weight)).ToList());
}

/// <summary>GET /api/portfolio/models — every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var models = await svc.ListModelsAsync(ct);
                return Results.Ok(models.Select(UpsertModelEndpoint.ToResponse).ToList());
            })
            .Produces<List<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>PUT /api/portfolio/investor/model — the investor chooses which model their portfolio follows.</summary>
public class SelectModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (SelectModelRequest request, HttpContext http, IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var shopper = PortfolioUser.Name(http.User);
                await svc.SelectModelAsync(shopper, request.ModelId, ct);
                return Results.NoContent();
            })
            .WithTags("PortfolioEndpoints");
    }
}
