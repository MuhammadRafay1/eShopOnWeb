using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PortfolioViewService : IPortfolioViewService
{
    private readonly IRepository<Investor> _investors;
    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IUpvestClient _upvest;
    private readonly IAppLogger<PortfolioViewService> _logger;

    public PortfolioViewService(
        IRepository<Investor> investors,
        IRepository<PortfolioPayment> payments,
        IUpvestClient upvest,
        IAppLogger<PortfolioViewService> logger)
    {
        _investors = investors;
        _payments = payments;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<PortfolioView?> GetAsync(string shopUserName, CancellationToken ct = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopUserSpecification(shopUserName), ct);
        if (investor is null) return null;

        // Total paid in, from this shopper's own pay-ins.
        var payments = await _payments.ListAsync(new PaymentsByShopUserSpecification(shopUserName), ct);
        var paidIn = payments.Sum(p => p.Amount);

        var holdings = new List<PortfolioHolding>();
        decimal totalValue = 0m;

        if (investor.AccountId is not null)
        {
            try
            {
                var valuation = await _upvest.GetCurrentValuationAsync(investor.AccountId, ct);
                if (valuation is not null)
                {
                    totalValue = valuation.TotalValue;
                    holdings = valuation.Positions
                        .Select(p => new PortfolioHolding(p.InstrumentId, p.Quantity, p.Value ?? 0m))
                        .ToList();
                }
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not retrieve valuation (HTTP {Status}: {Detail}).", ex.StatusCode, ex.Message);
            }
        }

        return new PortfolioView(investor.ModelId, holdings, totalValue, paidIn);
    }
}
