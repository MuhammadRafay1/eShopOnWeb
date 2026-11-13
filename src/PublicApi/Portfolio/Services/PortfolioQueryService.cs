using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.PublicApi.Portfolio.Data;
using Microsoft.eShopWeb.PublicApi.Portfolio.Domain;
using Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

/// <summary>One holding in the caller's portfolio view.</summary>
public sealed record HoldingView(string InstrumentId, decimal Quantity, decimal Value);

/// <summary>The caller's portfolio: model followed, holdings, total value, and total paid in.</summary>
public sealed record PortfolioView(string? ModelId, IReadOnlyList<HoldingView> Holdings, decimal TotalValue, decimal PaidIn);

/// <summary>
/// Flow 5 — the investor's portfolio as Upvest currently records it, combined with the model the investor follows and
/// the total they have paid in (from the local payment records).
/// </summary>
public sealed class PortfolioQueryService
{
    private readonly PortfolioContext _db;
    private readonly IUpvestGateway _gateway;
    private readonly ILogger<PortfolioQueryService> _logger;

    public PortfolioQueryService(PortfolioContext db, IUpvestGateway gateway, ILogger<PortfolioQueryService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken ct)
    {
        var investor = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct);
        if (investor is null)
            return null;

        // Total paid in = the caller's pay-ins that have not failed (pending or invested), summed from the local store.
        var paidIn = await _db.Payments
            .Where(p => p.ShopperId == shopperId && p.Status != PaymentStatus.Failed)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var holdings = new List<HoldingView>();
        decimal totalValue = 0m;

        if (investor.AccountId is { } accountId)
        {
            try
            {
                var valuation = await _gateway.GetValuationAsync(accountId, ct);
                totalValue = valuation.TotalValue;
                holdings = valuation.Holdings
                    .Select(h => new HoldingView(h.InstrumentId, h.Quantity, h.Value ?? 0m))
                    .ToList();
            }
            catch (UpvestProviderException ex)
            {
                // The sandbox client cannot read valuations; report the model and paid-in with no live holdings.
                _logger.LogWarning("Portfolio for {ShopperId}: valuation unavailable ({Status}); returning model and paid-in only.",
                    shopperId, (int?)ex.StatusCode);
            }
        }

        return new PortfolioView(investor.ChosenModelId, holdings, UpvestAmounts.ToMoney(totalValue), UpvestAmounts.ToMoney(paidIn));
    }
}
