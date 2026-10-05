using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Investing.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Advances the asynchronous parts of the capability. It keeps each shopper's state in step with
/// Upvest two ways that reinforce each other: this is called both from the periodic background
/// sweep and, for a single resource, from inbound webhooks. Upvest is always treated as the source
/// of truth — webhooks only trigger a re-read, they are never trusted blindly.
///
/// The concurrency guard is held only around the short database read-modify-write sections, never
/// around the Upvest network calls, so the order path's set-aside never waits on slow I/O.
/// </summary>
public sealed class InvestingProcessor : IInvestingProcessor
{
    private static readonly TimeSpan AbandonedInvestmentGrace = TimeSpan.FromSeconds(30);

    private readonly IRepository<InvestingAccount> _repository;
    private readonly IUpvestInvestmentClient _upvest;
    private readonly InvestingConcurrencyGuard _guard;
    private readonly ILogger<InvestingProcessor> _logger;

    public InvestingProcessor(
        IRepository<InvestingAccount> repository,
        IUpvestInvestmentClient upvest,
        InvestingConcurrencyGuard guard,
        ILogger<InvestingProcessor> logger)
    {
        _repository = repository;
        _upvest = upvest;
        _guard = guard;
        _logger = logger;
    }

    public async Task<decimal> SetAsideForPaidOrderAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken cancellationToken = default)
    {
        using (await _guard.AcquireAsync(cancellationToken))
        {
            var account = await _repository.FirstOrDefaultAsync(new InvestingAccountByBuyerSpec(buyerId), cancellationToken);
            if (account is null || !account.IsAcceptedInvestor)
            {
                return 0m;
            }

            var roundUp = account.SetAsideForOrder(orderId, orderTotal);
            if (roundUp > 0m)
            {
                await _repository.UpdateAsync(account, cancellationToken);
            }
            return roundUp;
        }
    }

    public async Task ReconcilePendingEnrolmentsAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _repository.ListAsync(new PendingEnrolmentsSpec(), cancellationToken);
        foreach (var account in pending)
        {
            await ReconcileEnrolmentAsync(account, cancellationToken);
        }
    }

    public async Task ReconcileEnrolmentByUpvestUserAsync(string upvestUserId, CancellationToken cancellationToken = default)
    {
        var account = await _repository.FirstOrDefaultAsync(new InvestingAccountByUpvestUserSpec(upvestUserId), cancellationToken);
        if (account is not null)
        {
            await ReconcileEnrolmentAsync(account, cancellationToken);
        }
    }

    private async Task ReconcileEnrolmentAsync(InvestingAccount account, CancellationToken cancellationToken)
    {
        if (account.Status != EnrolmentStatus.Pending || string.IsNullOrEmpty(account.UpvestUserId))
        {
            return;
        }

        try
        {
            var userStatus = await _upvest.GetUserStatusAsync(account.UpvestUserId!, cancellationToken);
            _logger.LogInformation("Enrolment {EnrolmentId}: Upvest user status {Status}.", account.EnrolmentId, userStatus);

            if (IsOffboarded(userStatus))
            {
                await MutateAsync(account, a => a.MarkRejected(), cancellationToken);
                _logger.LogInformation("Enrolment {EnrolmentId} rejected (user status {Status}).", account.EnrolmentId, userStatus);
                return;
            }

            // Upvest accepts the user asynchronously once all checks pass; only then can an account
            // group and trading account be created.
            if (!string.Equals(userStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrEmpty(account.UpvestAccountId))
            {
                var group = await _upvest.CreateAccountGroupAsync(account.UpvestUserId!, cancellationToken);
                var tradingAccount = await _upvest.CreateAccountAsync(account.UpvestUserId!, group.Id, cancellationToken);
                await MutateAsync(account, a => a.LinkUpvestAccounts(group.Id, tradingAccount.Id), cancellationToken);
                _logger.LogInformation("Enrolment {EnrolmentId}: created account group {Group} and account {Account}.",
                    account.EnrolmentId, group.Id, tradingAccount.Id);
            }

            var accountStatus = await _upvest.GetAccountStatusAsync(account.UpvestAccountId!, cancellationToken);
            _logger.LogInformation("Enrolment {EnrolmentId}: Upvest account status {Status}.", account.EnrolmentId, accountStatus);
            if (string.Equals(accountStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                await MutateAsync(account, a => a.MarkActive(), cancellationToken);
                _logger.LogInformation("Enrolment {EnrolmentId} accepted by Upvest.", account.EnrolmentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reconcile enrolment {EnrolmentId}.", account.EnrolmentId);
        }
    }

    public async Task ExecuteReadyInvestmentsAsync(CancellationToken cancellationToken = default)
    {
        var ready = await _repository.ListAsync(new ReadyToInvestAccountsSpec(), cancellationToken);
        foreach (var account in ready)
        {
            Guid investmentId;
            decimal amount;
            string accountGroupId;
            string accountId;
            string userId;

            using (await _guard.AcquireAsync(cancellationToken))
            {
                if (!account.IsReadyToInvest)
                {
                    continue;
                }
                var investment = account.BeginInvestment();
                await _repository.UpdateAsync(account, cancellationToken);

                investmentId = investment.PublicId;
                amount = investment.Amount;
                accountGroupId = account.UpvestAccountGroupId!;
                accountId = account.UpvestAccountId!;
                userId = account.UpvestUserId!;
            }

            try
            {
                // Fund the account group in the sandbox so the buy order has cash to settle against,
                // then invest the whole balance into the configured fund.
                await _upvest.IncreaseVirtualCashAsync(accountGroupId, amount, cancellationToken);
                var order = await _upvest.PlaceBuyOrderAsync(userId, accountId, amount, cancellationToken);

                await MutateAsync(account, a => a.AttachOrderToInvestment(investmentId, order.Id), cancellationToken);
                _logger.LogInformation("Placed investment {InvestmentId} (order {OrderId}) for {Amount} EUR.", investmentId, order.Id, amount);
            }
            catch (Exception ex)
            {
                // Return the amount to the set-aside balance so it accrues towards the next attempt.
                await MutateAsync(account, a => a.FailInvestment(investmentId), cancellationToken);
                _logger.LogError(ex, "Investment {InvestmentId} could not be placed; amount returned to balance.", investmentId);
            }
        }
    }

    public async Task ReconcilePendingInvestmentsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _repository.ListAsync(new AccountsWithPendingInvestmentsSpec(), cancellationToken);
        foreach (var account in accounts)
        {
            foreach (var investment in account.Investments)
            {
                if (investment.Status != InvestmentStatus.Pending)
                {
                    continue;
                }
                await ReconcileInvestmentAsync(account, investment, cancellationToken);
            }
        }
    }

    public async Task ReconcileInvestmentByUpvestOrderAsync(string upvestOrderId, CancellationToken cancellationToken = default)
    {
        var account = await _repository.FirstOrDefaultAsync(new InvestingAccountByUpvestOrderSpec(upvestOrderId), cancellationToken);
        if (account is null)
        {
            return;
        }
        foreach (var investment in account.Investments)
        {
            if (investment.Status == InvestmentStatus.Pending &&
                string.Equals(investment.UpvestOrderId, upvestOrderId, StringComparison.Ordinal))
            {
                await ReconcileInvestmentAsync(account, investment, cancellationToken);
            }
        }
    }

    private async Task ReconcileInvestmentAsync(InvestingAccount account, Investment investment, CancellationToken cancellationToken)
    {
        var investmentId = investment.PublicId;

        if (string.IsNullOrEmpty(investment.UpvestOrderId))
        {
            // Placed locally but never confirmed as sent to Upvest. If that state is stale, fail it
            // so the amount returns to the balance and can be retried.
            if (DateTimeOffset.UtcNow - investment.CreatedAt > AbandonedInvestmentGrace)
            {
                await MutateAsync(account, a => a.FailInvestment(investmentId), cancellationToken);
                _logger.LogWarning("Investment {InvestmentId} had no Upvest order; amount returned to balance.", investmentId);
            }
            return;
        }

        try
        {
            var order = await _upvest.GetOrderAsync(investment.UpvestOrderId!, cancellationToken);
            if (string.Equals(order.Status, "FILLED", StringComparison.OrdinalIgnoreCase))
            {
                await MutateAsync(account, a => a.SettleInvestment(investmentId), cancellationToken);
                _logger.LogInformation("Investment {InvestmentId} settled (order {OrderId}).", investmentId, order.Id);
            }
            else if (string.Equals(order.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                await MutateAsync(account, a => a.FailInvestment(investmentId), cancellationToken);
                _logger.LogInformation("Investment {InvestmentId} failed (order {OrderId} cancelled).", investmentId, order.Id);
            }
            // NEW / PROCESSING: still in flight; leave pending for a later pass.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reconcile investment {InvestmentId}.", investmentId);
        }
    }

    private async Task MutateAsync(InvestingAccount account, Action<InvestingAccount> mutate, CancellationToken cancellationToken)
    {
        using (await _guard.AcquireAsync(cancellationToken))
        {
            mutate(account);
            await _repository.UpdateAsync(account, cancellationToken);
        }
    }

    private static bool IsOffboarded(string status) =>
        status.StartsWith("OFFBOARD", StringComparison.OrdinalIgnoreCase);
}
