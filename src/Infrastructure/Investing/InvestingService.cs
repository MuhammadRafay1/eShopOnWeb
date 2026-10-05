using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

public sealed class InvestingService : IInvestingService
{
    // Serialises the "set aside / invest" critical section across the request
    // path and the background reconciliation sweep so a balance (which is
    // shared through the single in-memory store) is only ever invested once.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly CatalogContext _db;
    private readonly IUpvestClient _upvest;
    private readonly ILogger<InvestingService> _logger;

    public InvestingService(CatalogContext db, IUpvestClient upvest, ILogger<InvestingService> logger)
    {
        _db = db;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<decimal> HandleOrderPaidAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken ct)
    {
        try
        {
            var roundUp = SpareChangeCalculator.RoundUpToNextEuro(orderTotal);

            await Gate.WaitAsync(ct);
            try
            {
                var investor = await _db.Investors
                    .Include(i => i.Investments)
                    .FirstOrDefaultAsync(i => i.BuyerId == buyerId, ct);

                if (investor is null || investor.Status != EnrolmentStatus.Active)
                {
                    // Only an accepted investor sets anything aside.
                    return 0m;
                }

                var setAside = investor.SetAside(orderId, roundUp);
                await _db.SaveChangesAsync(ct);

                await InvestIfReadyAsync(investor, ct);
                return setAside;
            }
            finally
            {
                Gate.Release();
            }
        }
        catch (Exception ex)
        {
            // Investing must never break order placement.
            _logger.LogError(ex, "Setting aside spare change for order {OrderId} failed; order is unaffected.", orderId);
            return 0m;
        }
    }

    public async Task TryInvestAsync(Investor investor, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            // The investor may have been invested by the request path since it
            // was loaded for this sweep — refresh before deciding.
            await _db.Entry(investor).ReloadAsync(ct);
            await InvestIfReadyAsync(investor, ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    // Must be called while holding <see cref="Gate"/>.
    private async Task InvestIfReadyAsync(Investor investor, CancellationToken ct)
    {
        if (!investor.IsReadyToInvest() ||
            investor.UpvestAccountGroupId is null ||
            investor.UpvestAccountId is null ||
            investor.UpvestUserId is null)
        {
            return;
        }

        var amount = investor.PendingAmount;

        // Make the cash available, then invest the whole balance in one order.
        await _upvest.IncreaseVirtualCashAsync(investor.UpvestAccountGroupId, amount, ct);
        var order = await _upvest.PlaceBuyOrderAsync(investor.UpvestUserId, investor.UpvestAccountId, amount, ct);

        var investment = investor.BeginInvestment(order.Id);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Investment {InvestmentId} placed for enrolment {InvestorId} (Upvest order {OrderId}).",
            investment.Id, investor.Id, order.Id);
    }
}
