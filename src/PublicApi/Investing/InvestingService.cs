using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.PublicApi.Investing.Upvest;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Investing;

public sealed class InvestingService : IInvestingService
{
    /// <summary>Once the set-aside balance reaches this, the whole balance is invested.</summary>
    public const decimal InvestmentThresholdEuro = 10m;

    // Nationalities for which Upvest derives the regulatory identifier itself (CONCAT); no identifier call needed.
    private static readonly HashSet<string> ConcatCountries =
        new(StringComparer.OrdinalIgnoreCase) { "AT", "DE", "FR", "HU", "IE", "LU" };

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestApiClient _upvest;
    private readonly ILogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IRepository<Investment> investments,
        IUpvestApiClient upvest,
        ILogger<InvestingService> logger)
    {
        _investors = investors;
        _investments = investments;
        _upvest = upvest;
        _logger = logger;
    }

    public static decimal CalculateRoundUp(decimal total)
    {
        if (total <= 0m)
        {
            return 0m;
        }
        var roundUp = Math.Ceiling(total) - total;
        return decimal.Round(roundUp, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<EnrolmentResult> EnrolAsync(string buyerId, EnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            // Opting in is idempotent: return where the existing enrolment has got to.
            return ToEnrolmentResult(existing);
        }

        // Onboard the shopper as an investor with Upvest (regulatory steps, then account group + account).
        var user = await _upvest.CreateUserAsync(details, cancellationToken).ConfigureAwait(false);
        await _upvest.SubmitKycCheckAsync(user.Id, details, cancellationToken).ConfigureAwait(false);
        await _upvest.SubmitInstrumentFitCheckAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await _upvest.SetTaxResidenciesAsync(user.Id, details, cancellationToken).ConfigureAwait(false);

        if (!ConcatCountries.Contains(details.Nationality))
        {
            await _upvest.CreateNationalIdentifierAsync(user.Id, details, cancellationToken).ConfigureAwait(false);
        }

        // The user activates asynchronously once Upvest's checks pass; the trading account group and
        // account are created on activation by the reconciliation worker. The enrolment starts pending.
        var investor = new Investor(buyerId);
        investor.LinkUpvestUser(user.Id);

        await _investors.AddAsync(investor, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Shopper enrolled as investor; enrolment {EnrolmentId} status {Status}.",
            investor.EnrolmentId, investor.Status);
        return ToEnrolmentResult(investor);
    }

    public async Task<EnrolmentResult?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken).ConfigureAwait(false);
        return investor is null ? null : ToEnrolmentResult(investor);
    }

    public async Task<SetAsideResult> ApplyPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken = default)
    {
        var roundUp = CalculateRoundUp(orderTotal);

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken).ConfigureAwait(false);
        if (investor is null || !investor.IsAccepted || roundUp <= 0m)
        {
            // Not an accepted investor, or nothing to set aside.
            return new SetAsideResult(0m);
        }

        investor.SetAside(roundUp);

        if (investor.HasReachedInvestmentThreshold(InvestmentThresholdEuro))
        {
            var amount = investor.WithdrawSetAsideForInvestment();
            var investment = new Investment(investor.Id, amount);
            await _investments.AddAsync(investment, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Set-aside balance reached threshold; queued investment {InvestmentId} of {Amount} EUR.",
                investment.InvestmentId, amount);
        }

        await _investors.UpdateAsync(investor, cancellationToken).ConfigureAwait(false);
        return new SetAsideResult(roundUp);
    }

    public async Task<BalanceResult> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken).ConfigureAwait(false);
        if (investor is null)
        {
            return new BalanceResult(0m, 0m);
        }

        var investments = await _investments.ListAsync(new InvestmentsByInvestorSpecification(investor.Id), cancellationToken).ConfigureAwait(false);
        // Invested so far = everything moved out of the pending pot that has not failed back into it.
        var invested = investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.Amount);
        return new BalanceResult(decimal.Round(investor.PendingAmount, 2), decimal.Round(invested, 2));
    }

    public async Task<IReadOnlyList<InvestmentResult>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken).ConfigureAwait(false);
        if (investor is null)
        {
            return Array.Empty<InvestmentResult>();
        }

        var investments = await _investments.ListAsync(new InvestmentsByInvestorSpecification(investor.Id), cancellationToken).ConfigureAwait(false);
        return investments
            .Select(i => new InvestmentResult(i.InvestmentId, decimal.Round(i.Amount, 2), ToStatusString(i.Status)))
            .ToList();
    }

    private static EnrolmentResult ToEnrolmentResult(Investor investor) =>
        new(investor.EnrolmentId, ToStatusString(investor.Status));

    private static string ToStatusString(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    private static string ToStatusString(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending"
    };
}
