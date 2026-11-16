using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.PortfoliosRebalancing;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

public partial class UpvestPortfolioService
{
    private const int MaxRebalancingOrderPages = 100;

    // Flow 4 — realignment --------------------------------------------------------------------------

    public async Task<RealignmentResult> RealignAsync(string modelId, CancellationToken cancellationToken)
    {
        var model = await _repository.GetModelAsync(modelId, cancellationToken);
        if (model is null)
        {
            throw new ModelNotFoundException(modelId);
        }

        // A realignment is an intentional, repeatable operator action, so each trigger is a new execution.
        var response = await CallAsync("TriggerPortfolioRebalancing", c => _client.PortfoliosRebalancing.TriggerPortfolioRebalancing(
            new TriggerPortfolioRebalancingOperationRequest
            {
                IdempotencyKey = Guid.NewGuid(),
                Body = TriggerPortfolioRebalancingRequest.Allocations(
                    new Allocations { AllocationsValue = new[] { model.AllocationId } })
            }, cancellationToken: c), cancellationToken);

        var record = new RealignmentRecord
        {
            RealignmentId = response.Id,
            ModelId = modelId,
            AllocationId = model.AllocationId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repository.AddRealignmentAsync(record, cancellationToken);

        // The trigger response carries no status; it is derived from the execution's orders (none yet => pending).
        return new RealignmentResult(record.RealignmentId, RealignmentStatus.Pending);
    }

    public async Task<RealignmentResult> GetRealignmentAsync(Guid realignmentId, CancellationToken cancellationToken)
    {
        var record = await _repository.GetRealignmentAsync(realignmentId, cancellationToken);
        if (record is null)
        {
            throw new RealignmentNotFoundException(realignmentId);
        }

        var status = await DeriveRealignmentStatusAsync(realignmentId, cancellationToken);
        return new RealignmentResult(realignmentId, status);
    }

    /// <summary>
    /// The execution has no status field, so the realignment's status is derived from its orders: failed if any
    /// order was cancelled, completed once all orders are filled, otherwise pending.
    /// </summary>
    private async Task<RealignmentStatus> DeriveRealignmentStatusAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var orders = new List<PortfoliosRebalancingExecutionOrder>();
        int offset = 0;
        const int limit = 100;

        for (var page = 0; page < MaxRebalancingOrderPages; page++)
        {
            var currentOffset = offset;
            var response = await CallAsync("ListPortfolioRebalancingExecutionOrders", c =>
                _client.PortfoliosRebalancing.ListPortfolioRebalancingExecutionOrders(
                    new ListPortfolioRebalancingExecutionOrdersRequest { ExecutionId = executionId, Offset = currentOffset, Limit = limit },
                    cancellationToken: c), cancellationToken);

            orders.AddRange(response.Data);
            offset += response.Data.Count;
            if (response.Data.Count < limit || offset >= response.Meta.TotalCount)
            {
                break;
            }
        }

        if (orders.Count == 0)
        {
            return RealignmentStatus.Pending; // no orders produced yet
        }
        if (orders.Any(o => o.Status == Status71.Cancelled))
        {
            return RealignmentStatus.Failed;
        }
        if (orders.All(o => o.Status == Status71.Filled))
        {
            return RealignmentStatus.Completed;
        }
        return RealignmentStatus.Pending; // some still NEW / PROCESSING
    }
}
