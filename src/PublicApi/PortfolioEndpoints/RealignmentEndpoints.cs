using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 4 — operator realigns every investor who follows a model back to its current weights (admin).</summary>
public sealed class TriggerRealignmentEndpoint : PortfolioEndpointBase, IEndpoint<IResult, string>
{
    public TriggerRealignmentEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<TriggerRealignmentEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string modelId) => await HandleAsync(modelId))
            .Produces<RealignmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Results.BadRequest(new { error = "modelId is required." });
        }

        var model = Store.GetModel(modelId);
        if (model?.AllocationId is not { } allocationId)
        {
            return Results.NotFound(new { error = $"Model '{modelId}' does not exist." });
        }

        try
        {
            var idempotencyKey = Guid.NewGuid();
            var executionId = await Gateway.TriggerRealignmentAsync(allocationId, idempotencyKey, Ct);
            Store.SaveRealignment(new Realignment(executionId, modelId, idempotencyKey));

            var status = await Gateway.GetRealignmentStatusAsync(executionId, Ct);
            return Results.Accepted($"api/portfolio/realignments/{executionId}", new RealignmentResponse
            {
                RealignmentId = executionId.ToString(),
                Status = PortfolioShared.ToWire(status),
            });
        }
        catch (UpvestGatewayException ex)
        {
            Logger.LogWarning("Realignment of model '{ModelId}' failed (status {Status}).", modelId, ex.StatusCode);
            return PortfolioShared.ToProblem(ex);
        }
    }
}

/// <summary>Flow 4 — report where a realignment has got to (any signed-in shopper).</summary>
public sealed class GetRealignmentEndpoint : PortfolioEndpointBase, IEndpoint<IResult, string>
{
    public GetRealignmentEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<GetRealignmentEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (string realignmentId) => await HandleAsync(realignmentId))
            .Produces<RealignmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string realignmentId)
    {
        if (!Guid.TryParse(realignmentId, out var executionId))
        {
            return Results.BadRequest(new { error = "realignmentId must be a valid identifier." });
        }

        try
        {
            var status = await Gateway.GetRealignmentStatusAsync(executionId, Ct);
            return Results.Ok(new RealignmentResponse
            {
                RealignmentId = executionId.ToString(),
                Status = PortfolioShared.ToWire(status),
            });
        }
        catch (UpvestGatewayException ex)
        {
            return PortfolioShared.ToProblem(ex);
        }
    }
}
