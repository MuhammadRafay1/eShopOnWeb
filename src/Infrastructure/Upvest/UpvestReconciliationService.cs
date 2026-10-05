using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Drives every shopper's enrolment and investments to completion by polling Upvest, so that the
/// status held in this application always reflects what actually happened at Upvest. This is the
/// authoritative settlement mechanism (the webhook receiver is a best-effort supplement).
/// </summary>
public class UpvestReconciliationService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestReconciliationService> _logger;

    public UpvestReconciliationService(
        IServiceScopeFactory scopeFactory,
        IOptions<UpvestSettings> settings,
        ILogger<UpvestReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _logger.LogWarning("Upvest is not configured; the reconciliation service will not run.");
            return;
        }

        await RegisterWebhookAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upvest reconciliation tick failed.");
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task RegisterWebhookAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.CallbackBaseUrl)) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IUpvestClient>();
            var callbackUrl = _settings.CallbackBaseUrl.TrimEnd('/') + "/api/investing/upvest-events";
            await client.EnsureWebhookAsync(callbackUrl, cancellationToken);
            _logger.LogInformation("Ensured Upvest webhook subscription.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not register Upvest webhook; relying on polling.");
        }
    }

    private async Task ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Investor>>();
        var client = scope.ServiceProvider.GetRequiredService<IUpvestClient>();

        var investors = await repository.ListAsync(new InvestorsNeedingReconciliationSpecification(), cancellationToken);
        foreach (var investor in investors)
        {
            try
            {
                var changed = await AdvanceEnrolmentAsync(investor, client, cancellationToken);
                changed |= await AdvanceInvestmentsAsync(investor, client, cancellationToken);
                if (changed)
                    await repository.UpdateAsync(investor, cancellationToken);
            }
            catch (Exception ex)
            {
                // Isolate failures per investor; retry on the next tick.
                _logger.LogWarning("Reconciling enrolment {EnrolmentId} failed this tick: {Error}", investor.EnrolmentId, ex.GetType().Name);
            }
        }
    }

    private async Task<bool> AdvanceEnrolmentAsync(Investor investor, IUpvestClient client, CancellationToken cancellationToken)
    {
        if (investor.Status != EnrolmentStatus.Pending || investor.UpvestUserId is null)
            return false;

        // A failed regulatory check means Upvest will not take the shopper on.
        var checkStatuses = await client.GetCheckStatusesAsync(investor.UpvestUserId, cancellationToken);
        if (checkStatuses.Any(s => string.Equals(s, UpvestStatuses.CheckFailed, StringComparison.OrdinalIgnoreCase)))
        {
            investor.MarkRejected();
            _logger.LogInformation("Enrolment {EnrolmentId} was rejected by Upvest.", investor.EnrolmentId);
            return true;
        }

        var user = await client.GetUserAsync(investor.UpvestUserId, cancellationToken);
        if (!string.Equals(user.Status, UpvestStatuses.UserActive, StringComparison.OrdinalIgnoreCase))
            return false; // still onboarding

        var changed = false;

        if (investor.UpvestAccountGroupId is null)
        {
            var group = await client.CreateAccountGroupAsync(investor.UpvestUserId, cancellationToken);
            investor.SetUpvestAccountGroupId(group.Id);
            changed = true;
        }

        if (investor.UpvestAccountId is null && investor.UpvestAccountGroupId is not null)
        {
            var account = await client.CreateAccountAsync(investor.UpvestUserId, investor.UpvestAccountGroupId, cancellationToken);
            investor.SetUpvestAccountId(account.Id);
            changed = true;
        }

        if (investor.UpvestAccountId is not null)
        {
            var account = await client.GetAccountAsync(investor.UpvestAccountId, cancellationToken);
            if (string.Equals(account.Status, UpvestStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                investor.MarkActive();
                _logger.LogInformation("Enrolment {EnrolmentId} is now active.", investor.EnrolmentId);
                changed = true;
            }
        }

        return changed;
    }

    private async Task<bool> AdvanceInvestmentsAsync(Investor investor, IUpvestClient client, CancellationToken cancellationToken)
    {
        if (investor.UpvestUserId is null || investor.UpvestAccountId is null || investor.UpvestAccountGroupId is null)
            return false;

        var changed = false;
        foreach (var investment in investor.Investments.Where(i => i.Status == InvestmentStatus.Pending))
        {
            var amount = investment.AmountCents / 100m;

            if (investment.UpvestOrderId is null)
            {
                // Fund the account then place the buy order into the configured fund.
                await client.IncreaseVirtualCashAsync(investor.UpvestAccountGroupId, amount, cancellationToken);
                var order = await client.PlaceBuyOrderAsync(investor.UpvestUserId, investor.UpvestAccountId, amount, cancellationToken);
                investment.SetUpvestOrderId(order.Id);
                _logger.LogInformation("Placed investment {InvestmentId} ({Amount} EUR).", investment.PublicId, amount);
                changed = true;
                continue;
            }

            var current = await client.GetOrderAsync(investment.UpvestOrderId, cancellationToken);
            if (string.Equals(current.Status, UpvestStatuses.OrderFilled, StringComparison.OrdinalIgnoreCase))
            {
                investment.MarkSettled();
                _logger.LogInformation("Investment {InvestmentId} settled.", investment.PublicId);
                changed = true;
            }
            else if (string.Equals(current.Status, UpvestStatuses.OrderCancelled, StringComparison.OrdinalIgnoreCase))
            {
                investment.MarkFailed();
                investor.RefundFailedInvestment(investment);
                _logger.LogInformation("Investment {InvestmentId} failed; amount returned to balance.", investment.PublicId);
                changed = true;
            }
        }

        return changed;
    }
}
