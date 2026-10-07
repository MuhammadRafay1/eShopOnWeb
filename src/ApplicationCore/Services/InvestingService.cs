using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    private readonly IRepository<Investor> _investorRepository;
    private readonly IRepository<Investment> _investmentRepository;
    private readonly IUpvestGateway _upvest;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investorRepository,
        IRepository<Investment> investmentRepository,
        IUpvestGateway upvest,
        IAppLogger<InvestingService> logger)
    {
        _investorRepository = investorRepository;
        _investmentRepository = investmentRepository;
        _upvest = upvest;
        _logger = logger;
    }

    // How long EnrolAsync waits for Upvest to accept the shopper before returning (acceptance is quick in
    // practice). If it has not happened by then, enrolment is returned as pending and GET enrolment completes it.
    private const int AcceptanceAttempts = 8;
    private static readonly TimeSpan AcceptancePollInterval = TimeSpan.FromSeconds(2);

    public async Task<Investor> EnrolAsync(string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);

        if (investor is null || string.IsNullOrEmpty(investor.UpvestUserId))
        {
            // Register with Upvest first; only persist once we have the Upvest user id, so a failed
            // registration leaves nothing half-created on our side.
            var upvestUserId = await _upvest.RegisterInvestorAsync(details, cancellationToken);

            if (investor is null)
            {
                investor = new Investor(buyerId);
                investor.LinkUpvestUser(upvestUserId);
                await _investorRepository.AddAsync(investor, cancellationToken);
            }
            else
            {
                investor.LinkUpvestUser(upvestUserId);
                await _investorRepository.UpdateAsync(investor, cancellationToken);
            }
        }

        // Give acceptance a short while to land so the caller usually sees "active" straight away.
        for (var attempt = 1; attempt <= AcceptanceAttempts && investor.Status != EnrolmentStatus.Active; attempt++)
        {
            await CompleteEnrolmentAsync(investor, cancellationToken);
            if (investor.Status != EnrolmentStatus.Pending)
            {
                break;
            }

            if (attempt < AcceptanceAttempts)
            {
                await Task.Delay(AcceptancePollInterval, cancellationToken);
            }
        }

        _logger.LogInformation("Investor {InvestorId} enrolled with Upvest, status {Status}.", investor.Id, investor.Status);
        return investor;
    }

    public async Task<Investor?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            return null;
        }

        // While not yet active, re-read where acceptance has got to at Upvest (and provision the holding account).
        if (investor.Status != EnrolmentStatus.Active && !string.IsNullOrEmpty(investor.UpvestUserId))
        {
            await CompleteEnrolmentAsync(investor, cancellationToken);
        }

        return investor;
    }

    private async Task CompleteEnrolmentAsync(Investor investor, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _upvest.TryCompleteEnrolmentAsync(investor.UpvestUserId!, cancellationToken);
            var changed = false;

            if (result.Status == EnrolmentStatus.Active && !string.IsNullOrEmpty(result.AccountGroupId) && !string.IsNullOrEmpty(result.AccountId)
                && string.IsNullOrEmpty(investor.UpvestAccountId))
            {
                investor.LinkHoldingAccount(result.AccountGroupId!, result.AccountId!);
                changed = true;
            }

            if (result.Status != investor.Status)
            {
                investor.UpdateStatus(result.Status);
                changed = true;
            }

            if (changed)
            {
                await _investorRepository.UpdateAsync(investor, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not advance enrolment for investor {InvestorId}: {Error}.", investor.Id, ex.Message);
        }
    }

    public async Task<decimal> SetAsideAndMaybeInvestAsync(Order order, CancellationToken cancellationToken = default)
    {
        var roundUp = SpareChange.RoundUp(order.Total());

        try
        {
            var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(order.BuyerId), cancellationToken);

            // Orders from a shopper who is not an accepted investor set nothing aside.
            if (investor is null || !investor.CanInvest)
            {
                return 0m;
            }

            if (roundUp > 0m)
            {
                investor.SetAside(roundUp);
                await _investorRepository.UpdateAsync(investor, cancellationToken);
            }

            if (investor.PendingAmount >= SpareChange.InvestmentThresholdEuros)
            {
                await InvestWholeBalanceAsync(investor, cancellationToken);
            }

            return roundUp;
        }
        catch (Exception ex)
        {
            // Placing an order must never fail because of anything to do with investing.
            _logger.LogWarning("Setting aside change for an order by investor failed and was ignored: {Error}.", ex.Message);
            return roundUp;
        }
    }

    private async Task InvestWholeBalanceAsync(Investor investor, CancellationToken cancellationToken)
    {
        var amount = investor.WithdrawPendingForInvestment();
        await _investorRepository.UpdateAsync(investor, cancellationToken);

        // Record the investment as pending before the external call, so the money is always accounted for.
        var investment = new Investment(investor.Id, amount);
        await _investmentRepository.AddAsync(investment, cancellationToken);

        try
        {
            var result = await _upvest.PlaceInvestmentOrderAsync(
                investor.UpvestUserId!, investor.UpvestAccountGroupId!, investor.UpvestAccountId!, amount, cancellationToken);

            investment.LinkUpvestOrder(result.OrderId);
            if (result.Status == InvestmentStatus.Settled)
            {
                investment.Settle();
            }
            else if (result.Status == InvestmentStatus.Failed)
            {
                investment.Fail();
                investor.ReturnToPending(amount);
                await _investorRepository.UpdateAsync(investor, cancellationToken);
            }

            await _investmentRepository.UpdateAsync(investment, cancellationToken);
            _logger.LogInformation("Placed investment {InvestmentId} of {Amount} for investor {InvestorId}, status {Status}.",
                investment.Id, amount, investor.Id, investment.Status);
        }
        catch (Exception)
        {
            // Could not place the order: fail the investment and return the money to the set-aside balance.
            investment.Fail();
            await _investmentRepository.UpdateAsync(investment, cancellationToken);
            investor.ReturnToPending(amount);
            await _investorRepository.UpdateAsync(investor, cancellationToken);
            throw;
        }
    }

    public async Task<InvestingBalance> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            return new InvestingBalance(0m, 0m);
        }

        var investments = await _investmentRepository.ListAsync(new InvestmentsByInvestorSpecification(investor.Id), cancellationToken);
        await ReconcilePendingAsync(investor, investments, cancellationToken);

        var invested = investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.Amount);
        return new InvestingBalance(decimal.Round(investor.PendingAmount, 2), decimal.Round(invested, 2));
    }

    public async Task<IReadOnlyList<Investment>> ListInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            return Array.Empty<Investment>();
        }

        var investments = await _investmentRepository.ListAsync(new InvestmentsByInvestorSpecification(investor.Id), cancellationToken);
        await ReconcilePendingAsync(investor, investments, cancellationToken);
        return investments;
    }

    public async Task ReconcileOrderAsync(string upvestOrderId, CancellationToken cancellationToken = default)
    {
        var investment = await _investmentRepository.FirstOrDefaultAsync(new InvestmentByUpvestOrderIdSpecification(upvestOrderId), cancellationToken);
        if (investment is null || investment.Status != InvestmentStatus.Pending)
        {
            return;
        }

        var investor = await _investorRepository.GetByIdAsync(investment.InvestorId, cancellationToken);
        await ReconcileOneAsync(investor, investment, cancellationToken);
    }

    private async Task ReconcilePendingAsync(Investor investor, IEnumerable<Investment> investments, CancellationToken cancellationToken)
    {
        foreach (var investment in investments.Where(i => i.Status == InvestmentStatus.Pending && !string.IsNullOrEmpty(i.UpvestOrderId)))
        {
            await ReconcileOneAsync(investor, investment, cancellationToken);
        }
    }

    private async Task ReconcileOneAsync(Investor? investor, Investment investment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(investment.UpvestOrderId))
        {
            return;
        }

        try
        {
            var status = await _upvest.GetOrderStatusAsync(investment.UpvestOrderId!, cancellationToken);
            if (status == InvestmentStatus.Settled)
            {
                investment.Settle();
                await _investmentRepository.UpdateAsync(investment, cancellationToken);
            }
            else if (status == InvestmentStatus.Failed)
            {
                investment.Fail();
                await _investmentRepository.UpdateAsync(investment, cancellationToken);
                if (investor is not null)
                {
                    investor.ReturnToPending(investment.Amount);
                    await _investorRepository.UpdateAsync(investor, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not reconcile investment {InvestmentId} against Upvest: {Error}.", investment.Id, ex.Message);
        }
    }
}
