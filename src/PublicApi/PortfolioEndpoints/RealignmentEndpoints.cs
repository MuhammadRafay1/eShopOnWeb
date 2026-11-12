using System;
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
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// POST /api/portfolio/models/{modelId}/realignments — realign every follower of a model (operator only).
/// </summary>
public class TriggerRealignmentEndpoint : IEndpoint<IResult, HttpContext>
{
    private readonly IUpvestInvestmentService _service;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IRepository<Realignment> _realignments;

    public TriggerRealignmentEndpoint(IUpvestInvestmentService service, IRepository<ModelPortfolio> models, IRepository<Realignment> realignments)
    {
        _service = service;
        _models = models;
        _realignments = realignments;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(http, ct);
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(HttpContext http) => HandleAsync(http, default);

    public async Task<IResult> HandleAsync(HttpContext http, CancellationToken ct)
    {
        var modelId = (string?)http.Request.RouteValues["modelId"];
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return Results.BadRequest(new { message = "A model id is required." });
        }

        // Cross-operation invariant: the model must be one the operator defined.
        var model = await _models.GetByIdAsync(modelId, ct);
        if (model is null)
        {
            return Results.NotFound(new { message = $"Unknown model '{modelId}'." });
        }
        if (model.UpvestAllocationId is not { } allocationId)
        {
            return Results.Conflict(new { message = "This model has no Upvest allocation yet; it cannot be realigned." });
        }

        var realignment = new Realignment(Guid.NewGuid(), modelId, Guid.NewGuid());
        try
        {
            await _realignments.AddAsync(realignment, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.Conflict(new { message = "A realignment is already being processed." });
        }

        try
        {
            var executionId = await _service.TriggerRealignmentAsync(allocationId, realignment.IdempotencyKey, ct);
            realignment.RecordExecution(executionId);
            await _realignments.UpdateAsync(realignment, ct);

            return Results.Created($"api/portfolio/realignments/{realignment.RealignmentId}", new RealignmentResponse
            {
                RealignmentId = realignment.RealignmentId.ToString(),
                Status = realignment.Status.ToApiString()
            });
        }
        catch (UpvestIntegrationException ex) when (ex.Kind == UpvestFailureKind.Unknown)
        {
            // Outcome unknown — the trigger may have landed. Leave the realignment pending for reconciliation.
            return Results.Accepted($"api/portfolio/realignments/{realignment.RealignmentId}", new RealignmentResponse
            {
                RealignmentId = realignment.RealignmentId.ToString(),
                Status = RealignmentStatus.Pending.ToApiString()
            });
        }
        catch (UpvestIntegrationException)
        {
            realignment.SetStatus(RealignmentStatus.Failed);
            await _realignments.UpdateAsync(realignment, ct);
            throw;
        }
    }
}

/// <summary>
/// GET /api/portfolio/realignments/{realignmentId} — where a realignment has got to (re-read from Upvest).
/// </summary>
public class GetRealignmentEndpoint : IEndpoint<IResult, HttpContext>
{
    private readonly IUpvestInvestmentService _service;
    private readonly IRepository<Realignment> _realignments;

    public GetRealignmentEndpoint(IUpvestInvestmentService service, IRepository<Realignment> realignments)
    {
        _service = service;
        _realignments = realignments;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string realignmentId, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(http, ct);
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(HttpContext http) => HandleAsync(http, default);

    public async Task<IResult> HandleAsync(HttpContext http, CancellationToken ct)
    {
        var raw = (string?)http.Request.RouteValues["realignmentId"];
        if (!Guid.TryParse(raw, out var realignmentId))
        {
            return Results.NotFound();
        }

        var realignment = await _realignments.GetByIdAsync(realignmentId, ct);
        if (realignment is null)
        {
            return Results.NotFound();
        }

        if (realignment.Status == RealignmentStatus.Pending && realignment.UpvestExecutionId is { } executionId)
        {
            try
            {
                var status = await _service.GetRealignmentStatusAsync(executionId, ct);
                if (status != realignment.Status)
                {
                    realignment.SetStatus(status);
                    await _realignments.UpdateAsync(realignment, ct);
                }
            }
            catch (UpvestIntegrationException)
            {
                // Keep the last-known status if the re-read is refused/unavailable.
            }
        }

        return Results.Ok(new RealignmentResponse
        {
            RealignmentId = realignment.RealignmentId.ToString(),
            Status = realignment.Status.ToApiString()
        });
    }
}
