using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.PublicApi.Investing.Upvest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Investing;

/// <summary>
/// Drives the asynchronous parts of investing, decoupled from any shopper request:
/// <list type="number">
/// <item>activates enrolments once Upvest has accepted the shopper;</item>
/// <item>places the buy order for queued investments (funding the account first);</item>
/// <item>settles investments, reflecting the order's actual outcome at Upvest.</item>
/// </list>
/// Webhooks update the same state when delivered; this poller guarantees correctness regardless.
/// </summary>
public sealed class InvestingReconciliationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UpvestOptions _options;
    private readonly ILogger<InvestingReconciliationService> _logger;
    private bool _webhookEnsured;

    public InvestingReconciliationService(
        IServiceScopeFactory scopeFactory,
        IOptions<UpvestOptions> options,
        ILogger<InvestingReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Investing reconciliation worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Investing reconciliation pass failed; will retry.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var upvest = provider.GetRequiredService<IUpvestApiClient>();
        var investors = provider.GetRequiredService<IRepository<Investor>>();
        var investments = provider.GetRequiredService<IRepository<Investment>>();

        if (!_webhookEnsured && !string.IsNullOrWhiteSpace(_options.CallbackBaseUrl))
        {
            await upvest.EnsureWebhookAsync(
                $"{_options.CallbackBaseUrl.TrimEnd('/')}/api/investing/upvest/webhook", ct).ConfigureAwait(false);
            _webhookEnsured = true;
        }

        var pendingInvestors = await investors.CountAsync(new InvestorsByStatusSpecification(InvestorStatus.Pending), ct).ConfigureAwait(false);
        var pendingInvestments = await investments.CountAsync(new InvestmentsByStatusSpecification(InvestmentStatus.Pending), ct).ConfigureAwait(false);
        _logger.LogDebug("Reconciliation tick: {Investors} pending investors, {Investments} pending investments.", pendingInvestors, pendingInvestments);

        await ActivatePendingInvestorsAsync(upvest, investors, ct).ConfigureAwait(false);
        await PlaceQueuedInvestmentsAsync(upvest, investors, investments, ct).ConfigureAwait(false);
        await SettlePlacedInvestmentsAsync(upvest, investors, investments, ct).ConfigureAwait(false);
    }

    private async Task ActivatePendingInvestorsAsync(IUpvestApiClient upvest, IRepository<Investor> investors, CancellationToken ct)
    {
        var pending = await investors.ListAsync(new InvestorsByStatusSpecification(InvestorStatus.Pending), ct).ConfigureAwait(false);
        foreach (var investor in pending)
        {
            if (string.IsNullOrEmpty(investor.UpvestUserId))
            {
                continue;
            }

            try
            {
                var userStatus = await upvest.GetUserStatusAsync(investor.UpvestUserId!, ct).ConfigureAwait(false);
                if (!string.Equals(userStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Enrolment {EnrolmentId}: Upvest user status {Status}.", investor.EnrolmentId, userStatus);
                    continue;
                }

                // The user is accepted. Ensure the trading account group and account exist (reuse any
                // Upvest has already created for the user), then activate once the account is live.
                if (string.IsNullOrEmpty(investor.UpvestAccountGroupId) || string.IsNullOrEmpty(investor.UpvestAccountId))
                {
                    var groupId = await upvest.FindAccountGroupIdAsync(investor.UpvestUserId!, ct).ConfigureAwait(false)
                        ?? await upvest.CreateAccountGroupAsync(investor.UpvestUserId!, ct).ConfigureAwait(false);
                    var accountId = await upvest.FindTradingAccountIdAsync(investor.UpvestUserId!, ct).ConfigureAwait(false)
                        ?? await upvest.CreateAccountAsync(investor.UpvestUserId!, groupId, ct).ConfigureAwait(false);
                    investor.LinkUpvestAccounts(groupId, accountId);
                    await investors.UpdateAsync(investor, ct).ConfigureAwait(false);
                }

                var accountStatus = await upvest.GetAccountStatusAsync(investor.UpvestAccountId!, ct).ConfigureAwait(false);
                if (!string.Equals(accountStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Enrolment {EnrolmentId}: Upvest account status {Status}.", investor.EnrolmentId, accountStatus);
                    continue;
                }

                investor.Activate();
                await investors.UpdateAsync(investor, ct).ConfigureAwait(false);
                _logger.LogInformation("Enrolment {EnrolmentId} activated.", investor.EnrolmentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reconcile activation for enrolment {EnrolmentId}.", investor.EnrolmentId);
            }
        }
    }

    private async Task PlaceQueuedInvestmentsAsync(IUpvestApiClient upvest, IRepository<Investor> investors, IRepository<Investment> investments, CancellationToken ct)
    {
        var pending = await investments.ListAsync(new InvestmentsByStatusSpecification(InvestmentStatus.Pending), ct).ConfigureAwait(false);
        foreach (var investment in pending)
        {
            if (!investment.IsAwaitingPlacement)
            {
                continue;
            }

            var investor = await investors.GetByIdAsync(investment.InvestorId, ct).ConfigureAwait(false);
            if (investor is null || !investor.IsAccepted ||
                string.IsNullOrEmpty(investor.UpvestAccountGroupId) ||
                string.IsNullOrEmpty(investor.UpvestAccountId) ||
                string.IsNullOrEmpty(investor.UpvestUserId))
            {
                continue;
            }

            try
            {
                // Fund the account with the amount to invest, then place the nominal buy order.
                await upvest.FundAccountGroupAsync(investor.UpvestAccountGroupId, investment.Amount, ct).ConfigureAwait(false);
                var orderId = await upvest.PlaceBuyOrderAsync(
                    investor.UpvestUserId, investor.UpvestAccountId, investment.Amount, ct).ConfigureAwait(false);

                investment.MarkPlaced(orderId);
                await investments.UpdateAsync(investment, ct).ConfigureAwait(false);
                _logger.LogInformation("Placed buy order for investment {InvestmentId}.", investment.InvestmentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not place order for investment {InvestmentId}; will retry.", investment.InvestmentId);
            }
        }
    }

    private async Task SettlePlacedInvestmentsAsync(IUpvestApiClient upvest, IRepository<Investor> investors, IRepository<Investment> investments, CancellationToken ct)
    {
        var pending = await investments.ListAsync(new InvestmentsByStatusSpecification(InvestmentStatus.Pending), ct).ConfigureAwait(false);
        foreach (var investment in pending)
        {
            if (string.IsNullOrEmpty(investment.UpvestOrderId))
            {
                continue;
            }

            try
            {
                var order = await upvest.GetOrderAsync(investment.UpvestOrderId, ct).ConfigureAwait(false);
                if (string.Equals(order.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    investment.MarkFailed();
                    await investments.UpdateAsync(investment, ct).ConfigureAwait(false);
                    await ReturnFailedAmountAsync(investors, investment, ct).ConfigureAwait(false);
                    _logger.LogInformation("Investment {InvestmentId} failed at Upvest.", investment.InvestmentId);
                }
                else if (string.Equals(order.Status, "FILLED", StringComparison.OrdinalIgnoreCase))
                {
                    investment.MarkSettled();
                    await investments.UpdateAsync(investment, ct).ConfigureAwait(false);
                    _logger.LogInformation("Investment {InvestmentId} settled at Upvest.", investment.InvestmentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reconcile settlement for investment {InvestmentId}.", investment.InvestmentId);
            }
        }
    }

    private static async Task ReturnFailedAmountAsync(IRepository<Investor> investors, Investment investment, CancellationToken ct)
    {
        var investor = await investors.GetByIdAsync(investment.InvestorId, ct).ConfigureAwait(false);
        if (investor is not null)
        {
            investor.ReturnFailedAmount(investment.Amount);
            await investors.UpdateAsync(investor, ct).ConfigureAwait(false);
        }
    }
}
