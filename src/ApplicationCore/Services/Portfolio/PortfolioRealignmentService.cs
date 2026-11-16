using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class PortfolioRealignmentService : IPortfolioRealignmentService
{
    private readonly IRepository<PortfolioRealignment> _realignments;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<PortfolioRealignmentService> _logger;

    public PortfolioRealignmentService(
        IRepository<PortfolioRealignment> realignments,
        IRepository<ModelPortfolio> models,
        IUpvestInvestmentGateway gateway,
        IAppLogger<PortfolioRealignmentService> logger)
    {
        _realignments = realignments;
        _models = models;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<RealignmentView> TriggerAsync(string modelKey, CancellationToken ct)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByKeySpecification(modelKey), ct);
        if (model is null)
        {
            throw new PortfolioValidationException($"Unknown model '{modelKey}'.");
        }
        if (model.UpvestAllocationId is null)
        {
            throw new PortfolioStateException($"Model '{modelKey}' is not yet available at the investment provider.");
        }

        // Duplicate-claim: write the realignment record (with its idempotency key) before the Upvest call.
        var realignment = new PortfolioRealignment(modelKey);
        realignment = await _realignments.AddAsync(realignment, ct);

        try
        {
            var executionId = await _gateway.TriggerRealignmentAsync(model.UpvestAllocationId.Value, realignment.IdempotencyKey, ct);
            realignment.SetExecution(executionId);
            realignment.SetStatus(RealignmentStatus.Pending);
            await _realignments.UpdateAsync(realignment, ct);
            _logger.LogInformation("Realignment {RealignmentId} triggered for model {ModelKey} (execution {ExecutionId}).",
                realignment.RealignmentKey, modelKey, executionId);
        }
        catch (UpvestGatewayException ex)
        {
            var status = ex.OutcomeUnknown ? RealignmentStatus.Pending : MapFailure(ex.StatusCode);
            realignment.SetStatus(status);
            await _realignments.UpdateAsync(realignment, ct);
            _logger.LogWarning("Realignment {RealignmentId} could not be triggered (status {Status}); recorded as {Mapped}.",
                realignment.RealignmentKey, ex.StatusCode, status);
        }

        return ToView(realignment);
    }

    public async Task<RealignmentView?> GetAsync(Guid realignmentKey, CancellationToken ct)
    {
        var realignment = await _realignments.FirstOrDefaultAsync(new RealignmentByKeySpecification(realignmentKey), ct);
        if (realignment is null)
        {
            return null;
        }

        if (realignment.Status == RealignmentStatus.Pending && realignment.UpvestExecutionId is not null)
        {
            try
            {
                var status = await _gateway.GetRealignmentStatusAsync(realignment.UpvestExecutionId.Value, ct);
                if (status != realignment.Status)
                {
                    realignment.SetStatus(status);
                    await _realignments.UpdateAsync(realignment, ct);
                }
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not reconcile realignment {RealignmentId} (status {Status}).", realignmentKey, ex.StatusCode);
            }
        }

        return ToView(realignment);
    }

    private static RealignmentStatus MapFailure(HttpStatusCode? statusCode) =>
        statusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict
            ? RealignmentStatus.Failed
            : RealignmentStatus.Pending;

    private static RealignmentView ToView(PortfolioRealignment r) => new(r.RealignmentKey, r.Status);
}
