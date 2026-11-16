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
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.Valuations;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

public partial class UpvestPortfolioService
{
    // Flow 5 — the investor's portfolio -------------------------------------------------------------

    public async Task<PortfolioResult> GetPortfolioAsync(string shopper, CancellationToken cancellationToken)
    {
        var investor = await _repository.GetInvestorAsync(shopper, cancellationToken);
        if (investor is null)
        {
            throw new InvestorNotFoundException();
        }

        // paidIn is application-derived (Upvest exposes no single "total contributed" figure): the sum of the
        // shopper's own pay-ins that were not refused.
        var payments = await _repository.ListPaymentsAsync(shopper, cancellationToken);
        var paidIn = payments.Where(p => p.Status != PaymentStatus.Failed).Sum(p => p.Amount);

        var holdings = new List<PortfolioHolding>();
        decimal totalValue = 0m;

        if (investor.UpvestAccountId is Guid accountId)
        {
            try
            {
                var valuation = await CallAsync("GetAccountValuation", c => _client.Valuations.GetAccountValuation(
                    new GetAccountValuationRequest { AccountId = accountId, PriceQuality = PriceQuality3.HighestAvailable },
                    cancellationToken: c), cancellationToken);

                totalValue = ParseDecimal(valuation.TotalSecurityValue.Amount);
                holdings = valuation.SecurityPositions
                    .Select(p => new PortfolioHolding(
                        p.Instrument.Isin ?? string.Empty,
                        ParseDecimal(p.Quantity),
                        p.Value is null ? 0m : ParseDecimal(p.Value.Amount)))
                    .ToList();
            }
            catch (UpvestApiException ex) when (IsAuthorizationFailure(ex))
            {
                _logger.LogWarning("Cannot read the portfolio valuation: the Upvest client lacks the required scope ({Status}).", ex.ProviderStatusCode);
            }
        }

        return new PortfolioResult(investor.ChosenModelId, holdings, totalValue, paidIn);
    }
}
