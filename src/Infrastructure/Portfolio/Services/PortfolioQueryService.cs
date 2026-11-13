using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Models.Enums;
using ReqValuations = UpvestInvestmentApi.Requests.Valuations;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio.Services;

/// <summary>
/// Flow 5 — the investor's portfolio as Upvest currently records it: the model it follows, each fund held with
/// its quantity and current value, the portfolio's total value (from the Upvest valuation), and the total paid in
/// (summed from the app's own payment records — the only part Upvest does not hold).
/// </summary>
public sealed class PortfolioQueryService : IPortfolioService
{
    private readonly UpvestInvestmentApiClient _client;
    private readonly PortfolioContext _db;
    private readonly ILogger<PortfolioQueryService> _logger;

    public PortfolioQueryService(UpvestInvestmentApiClient client, PortfolioContext db, ILogger<PortfolioQueryService> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    public async Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken ct)
    {
        var investor = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct);
        if (investor?.AccountId is null) return null;

        var paidIn = await _db.Payments
            .Where(p => p.ShopperId == shopperId && p.Status != PaymentStatus.Failed)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var holdings = new List<HoldingResult>();
        decimal totalValue = 0m;

        try
        {
            var valuation = await UpvestCall.ReadAsync(() => _client.Valuations.GetAccountValuation(
                new ReqValuations.GetAccountValuationRequest
                {
                    AccountId = investor.AccountId.Value,
                    PriceQuality = PriceQuality3.HighestAvailable
                }, cancellationToken: ct), "GetAccountValuation", _logger);

            totalValue = UpvestMapping.ParseDecimal(valuation.TotalSecurityValue.Amount);
            foreach (var pos in valuation.SecurityPositions)
            {
                var instrumentId = pos.Instrument.Isin ?? pos.Instrument.Uuid.ToString();
                var quantity = UpvestMapping.ParseDecimal(pos.Quantity);
                var value = pos.Value is null ? 0m : UpvestMapping.ParseDecimal(pos.Value.Amount);
                holdings.Add(new HoldingResult(instrumentId, quantity, value));
            }
        }
        catch (PortfolioProviderException ex)
        {
            // Valuation not available right now (scope/outage). Still return the model + paid-in the app owns.
            _logger.LogWarning("Valuation for the caller's portfolio is unavailable (status {Status}).", (int?)ex.StatusCode);
        }

        return new PortfolioView(investor.ChosenModelId, holdings, totalValue, paidIn);
    }
}
