using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// The shopper-facing investing operations. Every query is filtered by the
/// authenticated shopper's id, so one shopper can never see another's enrolment,
/// ledger or investments.
/// </summary>
public sealed class InvestingService : IInvestingService
{
    private readonly InvestingContext _db;
    private readonly IUpvestGateway _gateway;
    private readonly ILogger<InvestingService> _logger;

    public InvestingService(InvestingContext db, IUpvestGateway gateway, ILogger<InvestingService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<EnrolmentView> EnrolAsync(string buyerId, InvestorDetails details, CancellationToken ct)
    {
        var existing = await _db.Enrolments.FirstOrDefaultAsync(e => e.BuyerId == buyerId, ct);
        if (existing != null)
            return new EnrolmentView(existing.Id, existing.Status.ToWire());

        // Personal details flow straight to Upvest and are never persisted or logged here.
        var upvestUserId = await _gateway.CreateInvestorAsync(details, ct);

        var enrolment = new Enrolment(buyerId, upvestUserId);
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Enrolment {EnrolmentId} created; awaiting Upvest acceptance.", enrolment.Id);
        return new EnrolmentView(enrolment.Id, enrolment.Status.ToWire());
    }

    public async Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken ct)
    {
        var enrolment = await _db.Enrolments.AsNoTracking().FirstOrDefaultAsync(e => e.BuyerId == buyerId, ct);
        return enrolment == null ? null : new EnrolmentView(enrolment.Id, enrolment.Status.ToWire());
    }

    public async Task<decimal> SetAsideForPaidOrderAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken ct)
    {
        try
        {
            var enrolment = await _db.Enrolments.FirstOrDefaultAsync(e => e.BuyerId == buyerId, ct);
            if (enrolment is null || !enrolment.CanInvest)
                return 0m; // Only an accepted investor sets anything aside.

            var roundUp = Math.Round(Math.Ceiling(orderTotal) - orderTotal, 2, MidpointRounding.AwayFromZero);
            if (roundUp <= 0m)
                return 0m; // Whole-euro total sets nothing aside.

            _db.RoundUpEntries.Add(new RoundUpEntry(buyerId, orderId, roundUp));
            await _db.SaveChangesAsync(ct);
            return roundUp;
        }
        catch (Exception ex)
        {
            // Setting aside change must never fail the order.
            _logger.LogError(ex, "Failed to set aside change for order {OrderId}; order is unaffected.", orderId);
            return 0m;
        }
    }

    public async Task<BalanceView> GetBalanceAsync(string buyerId, CancellationToken ct)
    {
        var setAside = await _db.RoundUpEntries.Where(r => r.BuyerId == buyerId)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        var invested = await _db.Investments.Where(i => i.BuyerId == buyerId)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;

        var pending = Math.Round(setAside - invested, 2, MidpointRounding.AwayFromZero);
        return new BalanceView(pending, Math.Round(invested, 2, MidpointRounding.AwayFromZero));
    }

    public async Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken ct)
    {
        var investments = await _db.Investments.AsNoTracking()
            .Where(i => i.BuyerId == buyerId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .ToListAsync(ct);

        return investments
            .Select(i => new InvestmentView(i.Id, Math.Round(i.Amount, 2, MidpointRounding.AwayFromZero), i.Status.ToWire()))
            .ToList();
    }
}
