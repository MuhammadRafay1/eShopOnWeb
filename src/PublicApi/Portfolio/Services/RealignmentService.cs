using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

public interface IRealignmentService
{
    Task<RealignmentResult> TriggerAsync(string modelId, CancellationToken ct);
    Task<RealignmentResult?> GetAsync(Guid realignmentId, CancellationToken ct);
}

/// <summary>
/// Operator realignment: brings every portfolio that follows a model back to the model's current
/// weights, by triggering a portfolio rebalancing at Upvest on the model's allocation.
/// </summary>
public sealed class RealignmentService : IRealignmentService
{
    private readonly IRepository<Realignment> _realignments;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly ILogger<RealignmentService> _logger;

    public RealignmentService(
        IRepository<Realignment> realignments,
        IRepository<ModelPortfolio> models,
        IUpvestInvestmentGateway gateway,
        ILogger<RealignmentService> logger)
    {
        _realignments = realignments;
        _models = models;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<RealignmentResult> TriggerAsync(string modelId, CancellationToken ct)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct);
        if (model is null)
            throw new PortfolioValidationException($"Model '{modelId}' does not exist.", 404);
        if (!model.UpvestAllocationId.HasValue)
            throw new PortfolioValidationException($"Model '{modelId}' is not yet available at the investment provider.", 409);

        // Claim the realignment row (with its idempotency key) before the provider call.
        var realignment = new Realignment(Guid.NewGuid(), model.ModelId, model.UpvestAllocationId.Value);
        realignment = await _realignments.AddAsync(realignment, ct);

        try
        {
            var executionId = await _gateway.TriggerRealignmentAsync(model.UpvestAllocationId.Value, realignment.TriggerKey, ct);
            realignment.SetExecution(executionId);
            await _realignments.UpdateAsync(realignment, ct);
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning(ex, "Realignment {RealignmentId} did not complete (outcomeUnknown={Unknown}).", realignment.RealignmentId, ex.OutcomeUnknown);
            if (!ex.OutcomeUnknown)
                throw;
        }

        var state = await SafeStateAsync(realignment, ct);
        return new RealignmentResult(realignment.RealignmentId, state);
    }

    public async Task<RealignmentResult?> GetAsync(Guid realignmentId, CancellationToken ct)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByIdSpecification(realignmentId), ct);
        if (realignment is null)
            return null;
        var state = await SafeStateAsync(realignment, ct);
        return new RealignmentResult(realignment.RealignmentId, state);
    }

    private async Task<RealignmentState> SafeStateAsync(Realignment realignment, CancellationToken ct)
    {
        if (!realignment.UpvestExecutionId.HasValue)
            return ParseState(realignment.StatusCache);
        try
        {
            var state = await _gateway.GetRealignmentStateAsync(realignment.UpvestExecutionId.Value, ct);
            realignment.UpdateStatusCache(state.ToString().ToLowerInvariant());
            await _realignments.UpdateAsync(realignment, ct);
            return state;
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning(ex, "Could not read realignment status from Upvest; using last-known status.");
            return ParseState(realignment.StatusCache);
        }
    }

    private static RealignmentState ParseState(string cache) => cache switch
    {
        "completed" => RealignmentState.Completed,
        "failed" => RealignmentState.Failed,
        _ => RealignmentState.Pending,
    };
}
