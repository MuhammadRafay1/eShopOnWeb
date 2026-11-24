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
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>PUT /api/portfolio/models/{modelId} — operator creates or replaces a model portfolio.</summary>
public class UpsertModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, UpsertModelRequest request, IManagedPortfolioService service, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Results.BadRequest(new { message = "name is required." });
                if (request.Funds is null || request.Funds.Count == 0)
                    return Results.BadRequest(new { message = "funds must contain at least one entry." });

                var funds = request.Funds.Select(f => new FundInput(f.InstrumentId, f.Weight)).ToList();
                var result = await service.UpsertModelAsync(modelId, request.Name, funds, ct);
                return Results.Ok(result.ToResponse());
            })
            .Produces<ModelResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>GET /api/portfolio/models — every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IManagedPortfolioService service, CancellationToken ct) =>
            {
                var models = await service.GetModelsAsync(ct);
                return Results.Ok(models.Select(m => m.ToResponse()).ToList());
            })
            .Produces<List<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}
