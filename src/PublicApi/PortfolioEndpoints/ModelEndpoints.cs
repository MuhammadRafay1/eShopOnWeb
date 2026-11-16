using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 2 — PUT /api/portfolio/models/{modelId}: operator creates or replaces a model portfolio.</summary>
public class PutModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                       AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                string modelId,
                PutModelRequest request,
                IModelPortfolioService service,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return Results.BadRequest(new { message = "name is required." });
                if (request.Funds is null || request.Funds.Count == 0)
                    return Results.BadRequest(new { message = "funds must list at least one fund." });

                List<ModelFund> funds;
                try
                {
                    funds = request.Funds.Select(f => new ModelFund(f.InstrumentId, f.Weight)).ToList();
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }

                try
                {
                    var model = await service.UpsertAsync(modelId, request.Name!, funds, ct);
                    return Results.Ok(ToResponse(model));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (UpvestException ex)
                {
                    return PortfolioEndpointHelpers.ToResult(ex);
                }
            })
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    internal static ModelResponse ToResponse(ModelPortfolio m) => new()
    {
        ModelId = m.ModelId,
        Name = m.Name,
        Funds = m.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
    };
}

/// <summary>Flow 2 — GET /api/portfolio/models: every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                IModelPortfolioService service,
                CancellationToken ct) =>
            {
                var models = await service.ListAsync(ct);
                return Results.Ok(models.Select(PutModelEndpoint.ToResponse).ToList());
            })
            .Produces<List<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>Flow 2 — PUT /api/portfolio/investor/model: the signed-in investor chooses which model to follow.</summary>
public class ChooseModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ChooseModelRequest request,
                ClaimsPrincipal user,
                IModelPortfolioService service,
                CancellationToken ct) =>
            {
                var userName = PortfolioEndpointHelpers.GetUserName(user);
                if (string.IsNullOrEmpty(userName)) return Results.Unauthorized();
                if (string.IsNullOrWhiteSpace(request.ModelId))
                    return Results.BadRequest(new { message = "modelId is required." });

                try
                {
                    await service.ChooseModelAsync(userName, request.ModelId!, ct);
                    return Results.Ok(new { modelId = request.ModelId });
                }
                catch (InvestorNotFoundException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (ModelNotFoundException ex)
                {
                    return Results.NotFound(new { message = ex.Message });
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { message = ex.Message });
                }
                catch (UpvestException ex)
                {
                    return PortfolioEndpointHelpers.ToResult(ex);
                }
            })
            .WithTags("PortfolioEndpoints");
    }
}
