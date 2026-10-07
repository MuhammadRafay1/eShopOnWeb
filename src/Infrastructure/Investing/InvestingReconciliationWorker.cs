using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Drives the asynchronous parts of the flow by reconciling local state against
/// Upvest on a short interval:
///   * advances pending enrolments (user activation → account group → account → active);
///   * once a shopper's set-aside balance reaches €10, invests the whole balance;
///   * updates each investment's status to match its order's outcome at Upvest.
/// Every item is handled independently and defensively so one failure never stops
/// the rest or crashes the loop.
/// </summary>
public sealed class InvestingReconciliationWorker : BackgroundService
{
    private const decimal InvestThreshold = 10m;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvestingReconciliationWorker> _logger;

    public InvestingReconciliationWorker(IServiceScopeFactory scopeFactory, ILogger<InvestingReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<InvestingContext>();
                var gateway = scope.ServiceProvider.GetRequiredService<IUpvestGateway>();

                await AdvanceEnrolmentsAsync(db, gateway, stoppingToken);
                await TriggerInvestmentsAsync(db, gateway, stoppingToken);
                await ReconcileInvestmentsAsync(db, gateway, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Investing reconciliation tick failed; will retry.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task AdvanceEnrolmentsAsync(InvestingContext db, IUpvestGateway gateway, CancellationToken ct)
    {
        var pending = await db.Enrolments.Where(e => e.Status == EnrolmentStatus.Pending).ToListAsync(ct);
        foreach (var enrolment in pending)
        {
            try
            {
                var userStatus = await gateway.GetUserStatusAsync(enrolment.UpvestUserId, ct);
                if (userStatus is "OFFBOARDING" or "OFFBOARDED")
                {
                    enrolment.MarkRejected();
                    await db.SaveChangesAsync(ct);
                    continue;
                }
                if (userStatus != "ACTIVE")
                    continue; // still being accepted

                if (enrolment.AccountGroupId is null)
                    enrolment.AttachAccountGroup(await gateway.CreateAccountGroupAsync(enrolment.UpvestUserId, ct));

                if (enrolment.AccountGroupId is Guid groupId && enrolment.AccountId is null)
                {
                    var (accountId, _) = await gateway.CreateAccountAsync(enrolment.UpvestUserId, groupId, ct);
                    enrolment.AttachAccount(accountId);
                }

                if (enrolment.AccountId is Guid accountId2)
                {
                    var accountStatus = await gateway.GetAccountStatusAsync(accountId2, ct);
                    if (accountStatus == "ACTIVE")
                    {
                        enrolment.MarkActive();
                        _logger.LogInformation("Enrolment {EnrolmentId} is now active at Upvest.", enrolment.Id);
                    }
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not advance enrolment {EnrolmentId}.", enrolment.Id);
            }
        }
    }

    private async Task TriggerInvestmentsAsync(InvestingContext db, IUpvestGateway gateway, CancellationToken ct)
    {
        var active = await db.Enrolments.Where(e => e.Status == EnrolmentStatus.Active).ToListAsync(ct);
        foreach (var enrolment in active)
        {
            try
            {
                if (enrolment.AccountGroupId is not Guid groupId || enrolment.AccountId is not Guid accountId)
                    continue;

                var setAside = await db.RoundUpEntries.Where(r => r.BuyerId == enrolment.BuyerId)
                    .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
                var invested = await db.Investments.Where(i => i.BuyerId == enrolment.BuyerId)
                    .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;
                var pendingBalance = Math.Round(setAside - invested, 2, MidpointRounding.AwayFromZero);

                if (pendingBalance < InvestThreshold)
                    continue;

                // Reserve the whole balance as an investment before calling Upvest so the
                // next tick cannot double-invest it.
                var investment = new Investment(enrolment.BuyerId, pendingBalance);
                db.Investments.Add(investment);
                await db.SaveChangesAsync(ct);

                try
                {
                    await gateway.FundAsync(groupId, pendingBalance, ct);
                    var orderId = await gateway.PlaceInvestmentOrderAsync(enrolment.UpvestUserId, accountId, pendingBalance, ct);
                    investment.AttachOrder(orderId);
                    _logger.LogInformation("Placed investment {InvestmentId} of {Amount} EUR.", investment.Id, pendingBalance);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    investment.MarkFailed();
                    _logger.LogError(ex, "Investment {InvestmentId} could not be placed at Upvest.", investment.Id);
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not evaluate investment trigger for enrolment {EnrolmentId}.", enrolment.Id);
            }
        }
    }

    private async Task ReconcileInvestmentsAsync(InvestingContext db, IUpvestGateway gateway, CancellationToken ct)
    {
        var open = await db.Investments
            .Where(i => i.Status == InvestmentStatus.Pending && i.UpvestOrderId != null)
            .ToListAsync(ct);

        foreach (var investment in open)
        {
            try
            {
                var status = await gateway.GetOrderStatusAsync(investment.UpvestOrderId!.Value, ct);
                if (status == "FILLED")
                {
                    investment.MarkSettled();
                    await db.SaveChangesAsync(ct);
                }
                else if (status == "CANCELLED")
                {
                    investment.MarkFailed();
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not reconcile investment {InvestmentId}.", investment.Id);
            }
        }
    }
}
