using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class PortfolioQueryService : IPortfolioQueryService
{
    private readonly IPortfolioRepository<Investor> _investors;
    private readonly IPortfolioRepository<Payment> _payments;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<PortfolioQueryService> _logger;

    public PortfolioQueryService(
        IPortfolioRepository<Investor> investors,
        IPortfolioRepository<Payment> payments,
        IUpvestInvestmentGateway gateway,
        IAppLogger<PortfolioQueryService> logger)
    {
        _investors = investors;
        _payments = payments;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<PortfolioSummary?> GetAsync(string shopperId, CancellationToken cancellationToken)
    {
        var investor = await _investors.GetByIdAsync(shopperId, cancellationToken);
        if (investor is null)
        {
            return null;
        }

        // paidIn is the application's own sum of the investor's settled (invested) pay-ins, in euros —
        // not a Upvest field.
        var payments = await _payments.ListAsync(new PaymentsByShopperSpec(shopperId), cancellationToken);
        var paidIn = payments
            .Where(p => p.Status == PaymentStatus.Invested)
            .Sum(p => p.Amount);

        var holdings = new List<Holding>();
        decimal totalValue = 0m;

        if (investor.UpvestAccountId is Guid accountId)
        {
            try
            {
                // Holdings as Upvest currently records them: quantity from positions, value from the
                // account valuation's per-instrument security values; total from the valuation total.
                var positions = await _gateway.GetPositionsAsync(accountId, cancellationToken);
                var valuation = await _gateway.GetValuationAsync(accountId, cancellationToken);
                totalValue = valuation.TotalValue;

                holdings = positions
                    .Select(p => new Holding(
                        p.InstrumentId,
                        p.Quantity,
                        valuation.PerInstrumentValue.TryGetValue(p.InstrumentId, out var v) ? v : 0m))
                    .ToList();
            }
            catch (UpvestGatewayException ex)
            {
                // Degrade to what the application knows (model + paidIn) when the holdings/valuation
                // reads are not available (e.g. scopes not granted).
                _logger.LogWarning("Could not read portfolio holdings/valuation (status={Status}, scopeWall={ScopeWall}).", ex.StatusCode, ex.IsScopeWall);
            }
        }

        return new PortfolioSummary(investor.ChosenModelId, holdings, totalValue, paidIn);
    }
}
