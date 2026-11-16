using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class RealignmentService : IRealignmentService
{
    private readonly IRepository<Realignment> _realignments;
    private readonly IRepository<ModelPortfolio> _models;
    private readonly IUpvestClient _upvest;
    private readonly IAppLogger<RealignmentService> _logger;

    public RealignmentService(
        IRepository<Realignment> realignments,
        IRepository<ModelPortfolio> models,
        IUpvestClient upvest,
        IAppLogger<RealignmentService> logger)
    {
        _realignments = realignments;
        _models = models;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<Realignment> TriggerAsync(string modelId, CancellationToken ct = default)
    {
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByModelIdSpecification(modelId), ct)
            ?? throw new ModelNotFoundException($"Model portfolio '{modelId}' does not exist.");

        if (model.UpvestAllocationId is null)
            throw new InvalidOperationException("This model has no backing Upvest allocation, so its followers cannot be realigned.");

        // Rebalance every account linked to this model's allocation back to the current weights.
        var executionId = await _upvest.TriggerRebalancingAsync(model.UpvestAllocationId, ct);

        var realignment = new Realignment(modelId);
        realignment.SetExecution(executionId);
        await _realignments.AddAsync(realignment, ct);
        return realignment;
    }

    public async Task<Realignment?> GetAsync(int realignmentId, CancellationToken ct = default)
    {
        var realignment = await _realignments.GetByIdAsync(realignmentId, ct);
        if (realignment is null) return null;

        if (realignment.UpvestExecutionId is not null && realignment.Status == RealignmentStatus.Pending)
        {
            try
            {
                var orders = await _upvest.GetRebalancingOrdersAsync(realignment.UpvestExecutionId, ct);
                var derived = DeriveStatus(orders.Select(o => o.Status));
                if (derived != realignment.Status)
                {
                    realignment.SetStatus(derived);
                    await _realignments.UpdateAsync(realignment, ct);
                }
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not refresh realignment status (HTTP {Status}: {Detail}).", ex.StatusCode, ex.Message);
            }
        }

        return realignment;
    }

    private static RealignmentStatus DeriveStatus(System.Collections.Generic.IEnumerable<string> statuses)
    {
        var list = statuses.Select(s => s?.ToUpperInvariant()).ToList();
        if (list.Count == 0) return RealignmentStatus.Pending;
        if (list.Any(s => s is "NEW" or "PROCESSING")) return RealignmentStatus.Pending;
        if (list.Any(s => s is "FILLED")) return RealignmentStatus.Completed; // at least partly realigned
        return RealignmentStatus.Failed; // everything cancelled
    }
}
