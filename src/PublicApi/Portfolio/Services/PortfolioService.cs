using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

public interface IPortfolioService
{
    Task<PortfolioResult> GetAsync(string shopperId, CancellationToken ct);
}

/// <summary>
/// Reads the investor's portfolio as Upvest currently records it (holdings and their value, and the
/// total value), together with the model it follows and the amount paid in (summed from the app's own
/// invested payments).
/// </summary>
public sealed class PortfolioService : IPortfolioService
{
    private readonly IRepository<Investor> _investors;
    private readonly IReadRepository<Payment> _payments;
    private readonly IUpvestInvestmentGateway _gateway;

    public PortfolioService(
        IRepository<Investor> investors,
        IReadRepository<Payment> payments,
        IUpvestInvestmentGateway gateway)
    {
        _investors = investors;
        _payments = payments;
        _gateway = gateway;
    }

    public async Task<PortfolioResult> GetAsync(string shopperId, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpecification(shopperId), ct);
        if (investor is null || !investor.UpvestAccountId.HasValue)
            throw new PortfolioValidationException("You must be an accepted investor with a ready account to view a portfolio.", 409);

        var portfolio = await _gateway.GetPortfolioAsync(investor.UpvestAccountId.Value, ct);

        var payments = await _payments.ListAsync(new PaymentsByShopperIdSpecification(shopperId), ct);
        var paidIn = payments
            .Where(p => p.StatusCache == "invested")
            .Sum(p => p.Amount);

        return new PortfolioResult(investor.SelectedModelId, portfolio.Holdings, portfolio.TotalValue, paidIn);
    }
}
