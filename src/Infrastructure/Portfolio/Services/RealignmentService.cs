using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using ReqRebal = UpvestInvestmentApi.Requests.PortfoliosRebalancing;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio.Services;

/// <summary>
/// Flow 4 — realignment. Triggers a Upvest rebalancing of every account that follows a model's allocation, back
/// to the model's current weights. The rebalancing execution object carries no status of its own, so the status
/// is derived by aggregating the per-order statuses of the execution's orders (read across all pages).
/// </summary>
public sealed class RealignmentService : IRealignmentService
{
    private const int MaxPages = 100;

    private readonly UpvestInvestmentApiClient _client;
    private readonly PortfolioContext _db;
    private readonly ILogger<RealignmentService> _logger;

    public RealignmentService(UpvestInvestmentApiClient client, PortfolioContext db, ILogger<RealignmentService> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    public async Task<RealignmentResult> TriggerAsync(string modelId, CancellationToken ct)
    {
        var model = await _db.Models.FirstOrDefaultAsync(m => m.ModelId == modelId, ct);
        if (model is null)
            throw new PortfolioNotFoundException($"Model '{modelId}' does not exist.");
        if (model.AllocationId is null)
            throw new PortfolioConflictException(
                "This model has no Upvest allocation yet (no investor has followed it); nothing to realign.");

        var realignment = new Realignment
        {
            RealignmentId = Guid.NewGuid(),
            ModelId = modelId,
            AllocationId = model.AllocationId.Value,
            TriggerIdempotencyKey = Guid.NewGuid(),
            Status = RealignmentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Realignments.Add(realignment);
        await _db.SaveChangesAsync(ct);

        try
        {
            var response = await UpvestCall.WriteAsync(() => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(
                new ReqRebal.TriggerPortfolioRebalancingOperationRequest
                {
                    IdempotencyKey = realignment.TriggerIdempotencyKey,
                    Body = TriggerPortfolioRebalancingRequest.Allocations(new Allocations
                    {
                        AllocationsValue = new[] { model.AllocationId.Value }
                    })
                }, cancellationToken: ct), "TriggerPortfolioRebalancing", _logger);

            realignment.ExecutionId = response.Id;
            realignment.Status = await DeriveStatusAsync(response.Id, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (PortfolioUnknownOutcomeException)
        {
            _logger.LogWarning("Realignment {Id} outcome unknown; left pending for reconciliation.", realignment.RealignmentId);
        }
        catch (PortfolioProviderException ex)
        {
            _logger.LogWarning("Realignment {Id} failed at the provider (status {Status}).", realignment.RealignmentId, (int?)ex.StatusCode);
            realignment.Status = RealignmentStatus.Failed;
            await _db.SaveChangesAsync(ct);
        }

        return new RealignmentResult(realignment.RealignmentId, realignment.Status);
    }

    public async Task<RealignmentResult?> GetAsync(Guid realignmentId, CancellationToken ct)
    {
        var realignment = await _db.Realignments.FirstOrDefaultAsync(r => r.RealignmentId == realignmentId, ct);
        if (realignment is null) return null;

        if (realignment.Status == RealignmentStatus.Pending && realignment.ExecutionId is not null)
        {
            try
            {
                var status = await DeriveStatusAsync(realignment.ExecutionId.Value, ct);
                if (status != realignment.Status)
                {
                    realignment.Status = status;
                    await _db.SaveChangesAsync(ct);
                }
            }
            catch (PortfolioProviderException)
            {
                // can't reconcile now — report last-known (pending)
            }
        }

        return new RealignmentResult(realignment.RealignmentId, realignment.Status);
    }

    /// <summary>Aggregates the execution's per-order statuses (all pages) into a realignment status.</summary>
    private async Task<RealignmentStatus> DeriveStatusAsync(Guid executionId, CancellationToken ct)
    {
        var statuses = new List<Status71>();
        int offset = 0, pages = 0;
        while (true)
        {
            var page = await UpvestCall.ReadAsync(() => _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                new ReqRebal.ListPortfolioRebalancingExecutionOrdersRequest
                {
                    ExecutionId = executionId,
                    Offset = offset,
                    Limit = 100
                }, cancellationToken: ct), "ListPortfolioRebalancingExecutionOrders", _logger);

            statuses.AddRange(page.Data.Select(o => o.Status));
            offset = page.Meta.Offset + page.Meta.Count;
            if (page.Meta.Count == 0 || offset >= page.Meta.TotalCount || ++pages >= MaxPages)
                break;
        }

        if (statuses.Count == 0) return RealignmentStatus.Pending;
        if (statuses.Any(s => s == Status71.New || s == Status71.Processing)) return RealignmentStatus.Pending;
        if (statuses.All(s => s == Status71.Filled)) return RealignmentStatus.Completed;
        if (statuses.Any(s => s == Status71.Cancelled)) return RealignmentStatus.Failed;
        return RealignmentStatus.Completed;
    }
}
