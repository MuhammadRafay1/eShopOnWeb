using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Investment> _investments;
    private readonly IRepository<RoundUp> _roundUps;
    private readonly IUpvestInvestingGateway _upvest;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IRepository<Investment> investments,
        IRepository<RoundUp> roundUps,
        IUpvestInvestingGateway upvest,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _investments = investments;
        _roundUps = roundUps;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<EnrolmentView> EnrolAsync(string buyerId, InvestorSignUp signUp, CancellationToken cancellationToken = default)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (existing is not null)
        {
            // Opting in is idempotent — one shopper has one enrolment.
            return ToEnrolmentView(existing);
        }

        // Onboard at Upvest first; only persist once the investor truly exists there, so a
        // failed onboarding does not leave a stuck local record.
        var request = new UpvestEnrolmentRequest(
            signUp.FirstName,
            signUp.LastName,
            signUp.Email,
            signUp.BirthDate,
            signUp.Nationality,
            signUp.Address.Line1,
            signUp.Address.Postcode,
            signUp.Address.City,
            signUp.Address.Country,
            signUp.PhoneNumber,
            signUp.TaxId,
            signUp.TaxCountry);

        var result = await _upvest.EnrolInvestorAsync(request, cancellationToken);

        var investor = new Investor(buyerId);
        investor.LinkUpvestInvestor(result.UserId, result.AccountGroupId, result.AccountId);
        ApplyAcceptance(investor, result.Acceptance);

        await _investors.AddAsync(investor, cancellationToken);
        _logger.LogInformation("Investor enrolled for buyer {0}; enrolment {1} status {2}.", buyerId, investor.Id, investor.Status);
        return ToEnrolmentView(investor);
    }

    public async Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        return investor is null ? null : ToEnrolmentView(investor);
    }

    public async Task<decimal> HandlePaidOrderAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken cancellationToken = default)
    {
        decimal setAside = 0m;
        try
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);

            // Only an accepted investor sets anything aside.
            if (investor is null || !investor.IsAccepted)
            {
                return 0m;
            }

            var roundUpCents = RoundUpCents(orderTotal);
            if (roundUpCents == 0)
            {
                return 0m;
            }

            // Never set the same order aside twice.
            var already = await _roundUps.AnyAsync(new RoundUpByOrderSpecification(buyerId, orderId), cancellationToken);
            if (already)
            {
                return 0m;
            }

            await _roundUps.AddAsync(new RoundUp(buyerId, orderId, roundUpCents), cancellationToken);
            investor.SetAside(roundUpCents);
            await _investors.UpdateAsync(investor, cancellationToken);
            setAside = roundUpCents / 100m;

            await TryInvestAsync(investor, cancellationToken);
        }
        catch (Exception ex)
        {
            // Investing must never fail the order. Swallow and carry on.
            _logger.LogWarning("Setting change aside failed for buyer {0}, order {1}: {2}", buyerId, orderId, ex.Message);
        }

        return setAside;
    }

    private async Task TryInvestAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (!investor.ReadyToInvest || investor.UpvestUserId is null || investor.UpvestAccountId is null || investor.UpvestAccountGroupId is null)
        {
            return;
        }

        var amountCents = investor.WithdrawPendingForInvestment();
        await _investors.UpdateAsync(investor, cancellationToken);

        var investment = new Investment(investor.BuyerId, amountCents);
        await _investments.AddAsync(investment, cancellationToken);

        try
        {
            var placed = await _upvest.PlaceInvestmentOrderAsync(
                investor.UpvestUserId.Value,
                investor.UpvestAccountGroupId.Value,
                investor.UpvestAccountId.Value,
                amountCents / 100m,
                cancellationToken);

            investment.LinkUpvestOrder(placed.OrderId, placed.InstrumentId);
            await _investments.UpdateAsync(investment, cancellationToken);
            _logger.LogInformation("Invested {0} cents for buyer {1} as order {2}.", amountCents, investor.BuyerId, placed.OrderId);
        }
        catch (Exception ex)
        {
            // Placement failed: return the money to the set-aside balance and mark the attempt failed.
            investment.MarkFailed();
            await _investments.UpdateAsync(investment, cancellationToken);
            investor.ReturnFailedInvestment(amountCents);
            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogWarning("Placing investment failed for buyer {0}: {1}", investor.BuyerId, ex.Message);
        }
    }

    public async Task<BalanceView> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            return new BalanceView(0m, 0m);
        }

        return new BalanceView(investor.PendingAmountCents / 100m, investor.InvestedAmountCents / 100m);
    }

    public async Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investments = await _investments.ListAsync(new InvestmentsByBuyerIdSpecification(buyerId), cancellationToken);
        var views = new List<InvestmentView>(investments.Count);
        foreach (var investment in investments)
        {
            views.Add(new InvestmentView(investment.Id, investment.AmountCents / 100m, StatusText(investment.Status)));
        }
        return views;
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        // Enrolments awaiting acceptance.
        var pendingInvestors = await _investors.ListAsync(new PendingInvestorsSpecification(), cancellationToken);
        foreach (var investor in pendingInvestors)
        {
            try
            {
                var state = await _upvest.GetAccountAcceptanceAsync(investor.UpvestAccountId!.Value, cancellationToken);
                switch (state)
                {
                    case UpvestAcceptanceState.Accepted:
                        investor.MarkActive();
                        await _investors.UpdateAsync(investor, cancellationToken);
                        break;
                    case UpvestAcceptanceState.Rejected:
                        investor.MarkRejected();
                        await _investors.UpdateAsync(investor, cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Reconciling enrolment {0} failed: {1}", investor.Id, ex.Message);
            }
        }

        // Investments awaiting their outcome.
        var pendingInvestments = await _investments.ListAsync(new PendingInvestmentsSpecification(), cancellationToken);
        foreach (var investment in pendingInvestments)
        {
            try
            {
                var outcome = await _upvest.GetOrderOutcomeAsync(investment.UpvestOrderId!.Value, cancellationToken);
                if (outcome == UpvestInvestmentOutcome.Settled)
                {
                    investment.MarkSettled();
                    await _investments.UpdateAsync(investment, cancellationToken);
                }
                else if (outcome == UpvestInvestmentOutcome.Failed)
                {
                    investment.MarkFailed();
                    await _investments.UpdateAsync(investment, cancellationToken);
                    var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(investment.BuyerId), cancellationToken);
                    if (investor is not null)
                    {
                        investor.ReturnFailedInvestment(investment.AmountCents);
                        await _investors.UpdateAsync(investor, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Reconciling investment {0} failed: {1}", investment.Id, ex.Message);
            }
        }
    }

    private static long RoundUpCents(decimal orderTotal)
    {
        if (orderTotal <= 0m) return 0;
        var totalCents = (long)Math.Round(orderTotal * 100m, MidpointRounding.AwayFromZero);
        var remainder = totalCents % 100;
        return remainder == 0 ? 0 : 100 - remainder;
    }

    private static void ApplyAcceptance(Investor investor, UpvestAcceptanceState acceptance)
    {
        switch (acceptance)
        {
            case UpvestAcceptanceState.Accepted:
                investor.MarkActive();
                break;
            case UpvestAcceptanceState.Rejected:
                investor.MarkRejected();
                break;
            default:
                investor.MarkPending();
                break;
        }
    }

    private static EnrolmentView ToEnrolmentView(Investor investor)
        => new(investor.Id, StatusText(investor.Status));

    private static string StatusText(EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending"
    };

    private static string StatusText(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending"
    };
}
