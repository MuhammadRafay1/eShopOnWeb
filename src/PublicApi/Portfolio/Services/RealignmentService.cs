using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.PublicApi.Portfolio.Data;
using Microsoft.eShopWeb.PublicApi.Portfolio.Domain;
using Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

/// <summary>A realignment as returned to the caller.</summary>
public sealed record RealignmentResult(Guid RealignmentId, string Status);

/// <summary>
/// Flow 4 — realignment. The operator realigns every portfolio following a model back to the model's current weights,
/// via an Upvest rebalancing execution. Status is derived by polling the execution's orders (the execution itself
/// carries none).
/// </summary>
public sealed class RealignmentService
{
    private readonly PortfolioContext _db;
    private readonly IUpvestGateway _gateway;
    private readonly ILogger<RealignmentService> _logger;

    public RealignmentService(PortfolioContext db, IUpvestGateway gateway, ILogger<RealignmentService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<RealignmentResult> TriggerAsync(string modelId, CancellationToken ct)
    {
        var model = await _db.Models.FirstOrDefaultAsync(m => m.ModelId == modelId, ct)
            ?? throw new PortfolioRequestException($"Model '{modelId}' does not exist.", 404);
        if (model.AllocationId is null)
            throw new PortfolioRequestException($"Model '{modelId}' has not been mirrored to Upvest yet, so it cannot be realigned.", 409);

        // Claim first: the realignment row is written before the provider call.
        var realignment = new RealignmentRecord
        {
            RealignmentId = Guid.NewGuid(),
            ModelId = modelId,
            AllocationId = model.AllocationId.Value,
            Status = RealignmentStatus.Pending,
        };
        _db.Realignments.Add(realignment);
        await _db.SaveChangesAsync(ct);

        try
        {
            var executionId = await _gateway.TriggerRealignmentAsync(model.AllocationId.Value, realignment.IdempotencyKey, ct);
            realignment.UpvestExecutionId = executionId;
            realignment.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (UpvestProviderException ex)
        {
            _logger.LogWarning("Realignment {RealignmentId}: not triggered at Upvest ({Reason}); staying pending.",
                realignment.RealignmentId, ex.UnauthorizedScope ? "unauthorised scope" : (ex.OutcomeUnknown ? "unknown outcome" : $"status {(int?)ex.StatusCode}"));
        }

        return new RealignmentResult(realignment.RealignmentId, ToWire(realignment.Status));
    }

    public async Task<RealignmentResult?> GetStatusAsync(Guid realignmentId, CancellationToken ct)
    {
        var realignment = await _db.Realignments.FirstOrDefaultAsync(r => r.RealignmentId == realignmentId, ct);
        if (realignment is null)
            return null;

        if (realignment.UpvestExecutionId is { } executionId && realignment.Status == RealignmentStatus.Pending)
        {
            try
            {
                var status = await _gateway.GetRealignmentStatusAsync(executionId, ct);
                realignment.Status = FromWire(status);
                realignment.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            catch (UpvestProviderException ex)
            {
                _logger.LogWarning("Realignment {RealignmentId}: status not refreshed ({Status}).", realignmentId, (int?)ex.StatusCode);
            }
        }

        return new RealignmentResult(realignment.RealignmentId, ToWire(realignment.Status));
    }

    internal static RealignmentStatus FromWire(string status) => status switch
    {
        "completed" => RealignmentStatus.Completed,
        "failed" => RealignmentStatus.Failed,
        _ => RealignmentStatus.Pending,
    };

    internal static string ToWire(RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending",
    };
}
