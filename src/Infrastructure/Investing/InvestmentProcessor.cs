using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Processes started investments off the band of the shopper's order: funds the account group with the
/// invested amount, places the buy order for the configured fund, and settles the outcome. Keeping this off
/// the order path is what lets placing an order never fail for an investing reason.
/// </summary>
public sealed class InvestmentProcessor : BackgroundService
{
    private static readonly TimeSpan FundingSettleWait = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan SettlePollInterval = TimeSpan.FromSeconds(2);
    private const int SettlePollAttempts = 12;

    private readonly InvestmentQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvestmentProcessor> _logger;

    public InvestmentProcessor(InvestmentQueue queue, IServiceScopeFactory scopeFactory, ILogger<InvestmentProcessor> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var investmentId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(investmentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Leave the investment pending; GET /api/investing/investments reconciliation will settle it later.
                _logger.LogWarning(ex, "Investment {InvestmentId} processing deferred.", investmentId);
            }
        }
    }

    private async Task ProcessAsync(int investmentId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var investments = scope.ServiceProvider.GetRequiredService<IRepository<Investment>>();
        var investors = scope.ServiceProvider.GetRequiredService<IRepository<Investor>>();
        var gateway = scope.ServiceProvider.GetRequiredService<IUpvestGateway>();
        var reconciler = scope.ServiceProvider.GetRequiredService<IEnrolmentReconciler>();

        var investment = await investments.GetByIdAsync(investmentId, cancellationToken);
        if (investment is null || investment.Status != InvestmentStatus.Pending)
        {
            return;
        }
        var investor = await investors.GetByIdAsync(investment.InvestorId, cancellationToken);
        if (investor is null)
        {
            return;
        }

        // Defensive: make sure the account is active (the trigger already required it).
        if (!investor.AccountActive)
        {
            await reconciler.ReconcileAsync(investor, cancellationToken);
            investor = await investors.GetByIdAsync(investment.InvestorId, cancellationToken);
            if (investor is null || !investor.AccountActive || investor.AccountGroupId is null || investor.AccountId is null)
            {
                _logger.LogWarning("Investment {InvestmentId} not ready (account not active); will retry on next read.", investmentId);
                return;
            }
        }

        var euros = Money.ToEuros(investment.AmountCents);

        if (investment.UpvestOrderId is null)
        {
            if (!investment.Funded)
            {
                await gateway.FundAsync(investor.AccountGroupId!, euros, investment.TopupIdempotencyKey, cancellationToken);
                investment.MarkFunded();
                await investments.UpdateAsync(investment, cancellationToken);
                _logger.LogInformation("Investment {InvestmentId} funded with {Euros} EUR.", investmentId, euros);
                await Task.Delay(FundingSettleWait, cancellationToken);
            }

            var orderId = await gateway.PlaceInvestmentOrderAsync(
                investor.UpvestUserId!, investor.AccountId!, euros, investment.ClientReference, investment.OrderIdempotencyKey, cancellationToken);
            investment.LinkOrder(orderId);
            await investments.UpdateAsync(investment, cancellationToken);
            _logger.LogInformation("Investment {InvestmentId} order {OrderId} placed.", investmentId, orderId);
        }

        await SettleAsync(investments, investors, gateway, investment.Id, cancellationToken);
    }

    private async Task SettleAsync(
        IRepository<Investment> investments, IRepository<Investor> investors, IUpvestGateway gateway,
        int investmentId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < SettlePollAttempts; attempt++)
        {
            await Task.Delay(SettlePollInterval, cancellationToken);

            var investment = await investments.GetByIdAsync(investmentId, cancellationToken);
            if (investment is null || investment.Status != InvestmentStatus.Pending || investment.UpvestOrderId is null)
            {
                return;
            }

            UpvestOrderState state;
            try
            {
                state = await gateway.GetOrderAsync(investment.UpvestOrderId, cancellationToken);
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not read order for investment {InvestmentId}: {Reason}", investmentId, ex.Message);
                continue;
            }

            var investor = await investors.GetByIdAsync(investment.InvestorId, cancellationToken);
            if (investor is null)
            {
                return;
            }

            if (IsSettled(state.Status))
            {
                investment.MarkSettled();
                investor.RecordInvested(investment.AmountCents);
                await investments.UpdateAsync(investment, cancellationToken);
                await investors.UpdateAsync(investor, cancellationToken);
                _logger.LogInformation("Investment {InvestmentId} settled.", investmentId);
                return;
            }
            if (IsFailed(state.Status))
            {
                investment.MarkFailed();
                investor.ReturnFailedInvestment(investment.AmountCents);
                await investments.UpdateAsync(investment, cancellationToken);
                await investors.UpdateAsync(investor, cancellationToken);
                _logger.LogInformation("Investment {InvestmentId} failed at Upvest; amount returned to the set-aside balance.", investmentId);
                return;
            }
        }
        // Still pending — a later read of /api/investing/investments will reconcile it.
    }

    private static bool IsSettled(string status) =>
        status.Equals("FILLED", StringComparison.OrdinalIgnoreCase) || status.Equals("SETTLED", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailed(string status) =>
        status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) || status.Equals("REJECTED", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("EXPIRED", StringComparison.OrdinalIgnoreCase) || status.Equals("FAILED", StringComparison.OrdinalIgnoreCase);
}
