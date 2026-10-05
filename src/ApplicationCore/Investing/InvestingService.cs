using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Implements the "invest your change" flows. A shopper's enrolment, ledger and investments are
/// always scoped by <c>ShopperId</c>, so one shopper can never see another's. Personal details are
/// forwarded to Upvest at enrolment and are never persisted or logged here.
/// </summary>
public class InvestingService : IInvestingService
{
    /// <summary>Set-aside balance at which the whole balance is invested.</summary>
    public const decimal InvestmentThreshold = 10.00m;

    // Serialises mutations of a single shopper's ledger/investments across request and worker threads.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ShopperLocks = new();

    private readonly IRepository<InvestorEnrolment> _enrolments;
    private readonly IRepository<SpareChangeLedger> _ledgers;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestClient _upvest;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<InvestorEnrolment> enrolments,
        IRepository<SpareChangeLedger> ledgers,
        IRepository<Investment> investments,
        IUpvestClient upvest,
        IAppLogger<InvestingService> logger)
    {
        _enrolments = enrolments;
        _ledgers = ledgers;
        _investments = investments;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<InvestorEnrolment> EnrolAsync(string shopperId, UpvestInvestorRegistration registration, CancellationToken ct)
    {
        var gate = await LockAsync(shopperId, ct);
        try
        {
            var existing = await _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(shopperId), ct);
            if (existing is not null)
                return existing;

            var upvestUserId = await _upvest.CreateInvestorAsync(registration, ct);

            var enrolment = new InvestorEnrolment(shopperId);
            enrolment.SetUpvestUser(upvestUserId);
            await _enrolments.AddAsync(enrolment, ct);

            if (await _ledgers.FirstOrDefaultAsync(new LedgerByShopperSpecification(shopperId), ct) is null)
                await _ledgers.AddAsync(new SpareChangeLedger(shopperId), ct);

            _logger.LogInformation("Investor enrolment {EnrolmentId} created for shopper (pending acceptance).", enrolment.EnrolmentId);
            return enrolment;
        }
        finally { gate.Release(); }
    }

    public Task<InvestorEnrolment?> GetEnrolmentAsync(string shopperId, CancellationToken ct) =>
        _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(shopperId), ct);

    public async Task<decimal> HandlePaidOrderAsync(string shopperId, decimal orderTotal, CancellationToken ct)
    {
        // Investing must never cause order placement to fail.
        try
        {
            var roundUp = RoundUpOf(orderTotal);
            if (roundUp <= 0m)
                return 0m;

            var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(shopperId), ct);
            if (enrolment is null || !enrolment.IsAcceptedInvestor)
                return 0m; // only accepted investors set anything aside

            var gate = await LockAsync(shopperId, ct);
            try
            {
                var ledger = await _ledgers.FirstOrDefaultAsync(new LedgerByShopperSpecification(shopperId), ct);
                if (ledger is null)
                {
                    ledger = new SpareChangeLedger(shopperId);
                    await _ledgers.AddAsync(ledger, ct);
                }

                ledger.AddRoundUp(roundUp);

                if (ledger.PendingAmount >= InvestmentThreshold)
                {
                    var amount = ledger.WithdrawAll();
                    var investment = new Investment(shopperId, amount);
                    await _investments.AddAsync(investment, ct);
                    _logger.LogInformation("Set-aside balance reached threshold; investment {InvestmentId} of {Amount} EUR queued.", investment.InvestmentId, amount);
                }

                await _ledgers.UpdateAsync(ledger, ct);
            }
            finally { gate.Release(); }

            return roundUp;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Setting aside spare change failed for a paid order; the order is unaffected. {Error}", ex.Message);
            return 0m;
        }
    }

    public async Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string shopperId, CancellationToken ct) =>
        await _investments.ListAsync(new InvestmentsByShopperSpecification(shopperId), ct);

    public async Task<InvestingBalance> GetBalanceAsync(string shopperId, CancellationToken ct)
    {
        var ledger = await _ledgers.FirstOrDefaultAsync(new LedgerByShopperSpecification(shopperId), ct);
        var investments = await _investments.ListAsync(new InvestmentsByShopperSpecification(shopperId), ct);

        var pending = ledger?.PendingAmount ?? 0m;
        var invested = investments
            .Where(i => i.Status != InvestmentStatus.Failed)
            .Sum(i => i.Amount);

        return new InvestingBalance(decimal.Round(pending, 2), decimal.Round(invested, 2));
    }

    public async Task ProgressEnrolmentsAsync(CancellationToken ct)
    {
        var pending = await _enrolments.ListAsync(new PendingEnrolmentsSpecification(), ct);
        foreach (var enrolment in pending)
        {
            try { await ProgressEnrolmentAsync(enrolment, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning("Progressing enrolment {EnrolmentId} failed; will retry. {Error}", enrolment.EnrolmentId, ex.Message);
            }
        }
    }

    private async Task ProgressEnrolmentAsync(InvestorEnrolment enrolment, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(enrolment.UpvestUserId))
            return;

        var gate = await LockAsync(enrolment.ShopperId, ct);
        try
        {
            if (enrolment.Status != EnrolmentStatus.Pending)
                return;

            if (string.IsNullOrEmpty(enrolment.UpvestAccountId))
            {
                var userStatus = await _upvest.GetUserStatusAsync(enrolment.UpvestUserId!, ct);
                if (!string.Equals(userStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                    return; // still onboarding

                var account = await _upvest.CreateTradingAccountAsync(enrolment.UpvestUserId!, ct);
                enrolment.SetUpvestAccount(account.AccountGroupId, account.AccountId);
                if (string.Equals(account.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    enrolment.Activate();
                    _logger.LogInformation("Enrolment {EnrolmentId} accepted by Upvest.", enrolment.EnrolmentId);
                }
                await _enrolments.UpdateAsync(enrolment, ct);
            }
            else
            {
                var accountStatus = await _upvest.GetAccountStatusAsync(enrolment.UpvestAccountId!, ct);
                if (string.Equals(accountStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    enrolment.Activate();
                    await _enrolments.UpdateAsync(enrolment, ct);
                    _logger.LogInformation("Enrolment {EnrolmentId} accepted by Upvest.", enrolment.EnrolmentId);
                }
            }
        }
        finally { gate.Release(); }
    }

    public async Task ProcessInvestmentsAsync(CancellationToken ct)
    {
        var unsettled = await _investments.ListAsync(new UnsettledInvestmentsSpecification(), ct);
        foreach (var investment in unsettled)
        {
            try { await ProcessInvestmentAsync(investment, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning("Processing investment {InvestmentId} failed; will retry. {Error}", investment.InvestmentId, ex.Message);
            }
        }
    }

    private async Task ProcessInvestmentAsync(Investment investment, CancellationToken ct)
    {
        var gate = await LockAsync(investment.ShopperId, ct);
        try
        {
            if (investment.Status == InvestmentStatus.Created)
            {
                var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(investment.ShopperId), ct);
                if (enrolment is null || !enrolment.IsAcceptedInvestor)
                    return; // cannot invest until accepted

                await _upvest.TopUpAsync(enrolment.UpvestAccountGroupId!, investment.Amount, ct);
                var orderId = await _upvest.PlaceBuyOrderAsync(enrolment.UpvestUserId!, enrolment.UpvestAccountId!, investment.Amount, ct);
                investment.MarkPlaced(orderId);
                await _investments.UpdateAsync(investment, ct);
                _logger.LogInformation("Investment {InvestmentId} placed at Upvest as order {OrderId}.", investment.InvestmentId, orderId);
            }
            else if (investment.Status == InvestmentStatus.Placed && !string.IsNullOrEmpty(investment.UpvestOrderId))
            {
                var status = await _upvest.GetOrderStatusAsync(investment.UpvestOrderId!, ct);
                await ApplyOutcomeLocked(investment, status, ct);
            }
        }
        finally { gate.Release(); }
    }

    public async Task ApplyOrderOutcomeAsync(string upvestOrderId, string upvestStatus, CancellationToken ct)
    {
        var investment = await _investments.FirstOrDefaultAsync(new InvestmentByUpvestOrderIdSpecification(upvestOrderId), ct);
        if (investment is null)
            return;

        var gate = await LockAsync(investment.ShopperId, ct);
        try
        {
            await ApplyOutcomeLocked(investment, upvestStatus, ct);
        }
        finally { gate.Release(); }
    }

    private async Task ApplyOutcomeLocked(Investment investment, string upvestStatus, CancellationToken ct)
    {
        if (investment.IsTerminal)
            return;

        if (string.Equals(upvestStatus, "FILLED", StringComparison.OrdinalIgnoreCase))
        {
            investment.MarkSettled();
            await _investments.UpdateAsync(investment, ct);
            _logger.LogInformation("Investment {InvestmentId} settled.", investment.InvestmentId);
        }
        else if (string.Equals(upvestStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase))
        {
            investment.MarkFailed();
            await _investments.UpdateAsync(investment, ct);

            var ledger = await _ledgers.FirstOrDefaultAsync(new LedgerByShopperSpecification(investment.ShopperId), ct);
            if (ledger is null)
            {
                ledger = new SpareChangeLedger(investment.ShopperId);
                ledger.Restore(investment.Amount);
                await _ledgers.AddAsync(ledger, ct);
            }
            else
            {
                ledger.Restore(investment.Amount);
                await _ledgers.UpdateAsync(ledger, ct);
            }
            _logger.LogWarning("Investment {InvestmentId} failed at Upvest; amount returned to set-aside balance.", investment.InvestmentId);
        }
        // NEW / PROCESSING: leave as Placed and reconcile again later.
    }

    /// <summary>Round-up to the next whole euro, to two decimal places (0 if already a whole euro).</summary>
    public static decimal RoundUpOf(decimal total)
    {
        if (total <= 0m)
            return 0m;
        var roundUp = Math.Ceiling(total) - total;
        return decimal.Round(roundUp, 2, MidpointRounding.AwayFromZero);
    }

    private static async Task<SemaphoreSlim> LockAsync(string shopperId, CancellationToken ct)
    {
        var gate = ShopperLocks.GetOrAdd(shopperId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return gate;
    }
}
