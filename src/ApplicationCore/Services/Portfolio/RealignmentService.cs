using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class RealignmentService : IRealignmentService
{
    private readonly IPortfolioRepository<Realignment> _realignments;
    private readonly IPortfolioRepository<ModelPortfolio> _models;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<RealignmentService> _logger;

    public RealignmentService(
        IPortfolioRepository<Realignment> realignments,
        IPortfolioRepository<ModelPortfolio> models,
        IUpvestInvestmentGateway gateway,
        IAppLogger<RealignmentService> logger)
    {
        _realignments = realignments;
        _models = models;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<RealignmentSummary> TriggerAsync(string modelId, CancellationToken cancellationToken)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpec(modelId), cancellationToken);
        if (model is null)
        {
            throw new ModelPortfolioNotFoundException(modelId);
        }

        var realignment = new Realignment(Guid.NewGuid(), modelId);
        // Claim before the Upvest call.
        await _realignments.AddAsync(realignment, cancellationToken);

        if (model.UpvestAllocationId is Guid allocationId)
        {
            try
            {
                var executionId = await _gateway.TriggerRebalancingAsync(allocationId, realignment.IdempotencyKey, cancellationToken);
                realignment.SetExecutionId(executionId);
                var status = await _gateway.GetRebalancingStatusAsync(executionId, cancellationToken);
                realignment.SetStatus(Map(status));
            }
            catch (UpvestGatewayException ex) when (ex.IsScopeWall)
            {
                realignment.SetNote("Awaiting provider authorisation for rebalancing scopes.");
                _logger.LogWarning("Realignment paused at scope wall (stays pending).");
            }
            catch (UpvestGatewayException ex) when (ex.OutcomeUnknown)
            {
                realignment.SetNote("Realignment submitted; awaiting confirmation from the provider.");
                _logger.LogWarning("Realignment outcome unknown; will reconcile on next read.");
            }
            catch (UpvestGatewayException ex)
            {
                realignment.SetStatus(RealignmentStatus.Failed);
                realignment.SetNote("The provider rejected the realignment.");
                _logger.LogWarning("Realignment failed at the provider (status={Status}).", ex.StatusCode);
            }
        }
        else
        {
            realignment.SetNote("The model's Upvest allocation is not yet provisioned; realignment will run once it is.");
        }

        await _realignments.UpdateAsync(realignment, cancellationToken);
        return new RealignmentSummary(realignment.RealignmentId, realignment.Status);
    }

    public async Task<RealignmentSummary?> GetAsync(Guid realignmentId, CancellationToken cancellationToken)
    {
        var realignment = await _realignments.GetByIdAsync(realignmentId, cancellationToken);
        if (realignment is null)
        {
            return null;
        }

        if (realignment.Status == RealignmentStatus.Pending && realignment.UpvestExecutionId is Guid executionId)
        {
            try
            {
                var status = await _gateway.GetRebalancingStatusAsync(executionId, cancellationToken);
                realignment.SetStatus(Map(status));
                await _realignments.UpdateAsync(realignment, cancellationToken);
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not refresh realignment {RealignmentId} (status={Status}, scopeWall={ScopeWall}).", realignmentId, ex.StatusCode, ex.IsScopeWall);
            }
        }

        return new RealignmentSummary(realignment.RealignmentId, realignment.Status);
    }

    private static RealignmentStatus Map(UpvestRealignmentStatus status) => status switch
    {
        UpvestRealignmentStatus.Completed => RealignmentStatus.Completed,
        UpvestRealignmentStatus.Failed => RealignmentStatus.Failed,
        _ => RealignmentStatus.Pending
    };
}
