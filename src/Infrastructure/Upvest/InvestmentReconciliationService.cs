using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Keeps local state in step with Upvest. On a short interval it advances pending enrolments to
/// active or rejected, provisions the account that holds investments once a shopper is accepted,
/// invests the set-aside balance once it reaches the threshold, and settles investments to their
/// final state. All reads of Upvest happen outside the mutation gate; only the local
/// read-modify-write runs inside it, so the single-writer guarantee never spans a network call.
/// </summary>
public sealed class InvestmentReconciliationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUpvestGateway _gateway;
    private readonly IInvestorMutationGate _gate;
    private readonly ILogger<InvestmentReconciliationService> _logger;

    public InvestmentReconciliationService(
        IServiceScopeFactory scopeFactory,
        IUpvestGateway gateway,
        IInvestorMutationGate gate,
        ILogger<InvestmentReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _gateway = gateway;
        _gate = gate;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await WaitAsync(timer, stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await ReconcileEnrolmentsAsync(stoppingToken).ConfigureAwait(false);
                await ReconcileProvisioningAsync(stoppingToken).ConfigureAwait(false);
                await ReconcileInvestingAsync(stoppingToken).ConfigureAwait(false);
                await ReconcileInvestmentsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Reconciliation tick failed ({Error}); will retry.", ex.Message);
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return false; }
    }

    /// <summary>Runs a local read-modify-write against a fresh repository, serialised by the gate.</summary>
    private Task<T> MutateAsync<T>(Func<IRepository<Investor>, Task<T>> work, CancellationToken token) =>
        _gate.RunAsync(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<Investor>>();
            return await work(repo).ConfigureAwait(false);
        }, token);

    private async Task<IReadOnlyList<T>> ReadAsync<T>(Func<IRepository<Investor>, Task<List<T>>> query, CancellationToken token)
        => await MutateAsync(async repo => (IReadOnlyList<T>)await query(repo).ConfigureAwait(false), token).ConfigureAwait(false);

    private async Task ReconcileEnrolmentsAsync(CancellationToken token)
    {
        var pending = await ReadAsync(repo => repo.ListAsync(new PendingEnrolmentsSpecification(), token), token).ConfigureAwait(false);
        foreach (var snapshot in pending)
        {
            if (string.IsNullOrEmpty(snapshot.UpvestKycCheckId)) continue;
            try
            {
                var status = await _gateway.GetEnrolmentStatusAsync(snapshot.UpvestUserId, snapshot.UpvestKycCheckId, token).ConfigureAwait(false);
                if (status == EnrolmentStatus.Pending) continue;

                await MutateAsync(async repo =>
                {
                    var investor = await repo.FirstOrDefaultAsync(new InvestorByShopperSpecification(snapshot.ShopperId), token).ConfigureAwait(false);
                    if (investor is null || investor.Status != EnrolmentStatus.Pending) return false;
                    if (status == EnrolmentStatus.Active) investor.Activate();
                    else investor.Reject();
                    await repo.SaveChangesAsync(token).ConfigureAwait(false);
                    _logger.LogInformation("Enrolment {EnrolmentId} is now {Status}.", investor.PublicId, investor.Status);
                    return true;
                }, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Could not reconcile enrolment {EnrolmentId} ({Error}).", snapshot.PublicId, ex.Message);
            }
        }
    }

    private async Task ReconcileProvisioningAsync(CancellationToken token)
    {
        var unprovisioned = await ReadAsync(repo => repo.ListAsync(new ActiveUnprovisionedInvestorsSpecification(), token), token).ConfigureAwait(false);
        foreach (var snapshot in unprovisioned)
        {
            try
            {
                var account = await _gateway.ProvisionAccountAsync(snapshot.UpvestUserId, token).ConfigureAwait(false);
                if (string.IsNullOrEmpty(account.AccountGroupId) || string.IsNullOrEmpty(account.AccountId)) continue;

                await MutateAsync(async repo =>
                {
                    var investor = await repo.FirstOrDefaultAsync(new InvestorByShopperSpecification(snapshot.ShopperId), token).ConfigureAwait(false);
                    if (investor is null || investor.IsProvisioned) return false;
                    investor.SetAccounts(account.AccountGroupId, account.AccountId);
                    await repo.SaveChangesAsync(token).ConfigureAwait(false);
                    _logger.LogInformation("Provisioned account for enrolment {EnrolmentId}.", investor.PublicId);
                    return true;
                }, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Could not provision account for enrolment {EnrolmentId} ({Error}).", snapshot.PublicId, ex.Message);
            }
        }
    }

    private async Task ReconcileInvestingAsync(CancellationToken token)
    {
        // Phase 1 (gated, local): choose one investor ready to invest and begin the investment,
        // resetting the set-aside balance. The one-in-flight guard means a balance is never
        // invested twice even if later state reads lag.
        var begun = await MutateAsync(async repo =>
        {
            var active = await repo.ListAsync(new ActiveInvestorsSpecification(), token).ConfigureAwait(false);
            foreach (var investor in active)
            {
                if (!investor.CanBeginInvestment(InvestingService.InvestmentThreshold)) continue;

                var investment = investor.StartInvestment();
                await repo.SaveChangesAsync(token).ConfigureAwait(false);     // persist the new investment
                investor.ResetPendingForInvestment(investment.Amount);
                await repo.SaveChangesAsync(token).ConfigureAwait(false);     // persist the balance reset separately
                return new BegunInvestment(investor.ShopperId, investor.UpvestAccountGroupId!, investor.UpvestAccountId!, investment.PublicId, investment.Amount);
            }
            return null;
        }, token).ConfigureAwait(false);

        if (begun is null) return;

        // Phase 2 (ungated, network): fund and place the order with Upvest.
        UpvestInvestmentResult? result = null;
        string? error = null;
        try
        {
            result = await _gateway.InvestAsync(begun.AccountGroupId, begun.AccountId, begun.Amount, token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = ex.Message;
        }

        // Phase 3 (gated, local): record the order on the investment, or fail it (returning the
        // amount to the set-aside balance for a later retry).
        await MutateAsync(async repo =>
        {
            var investor = await repo.FirstOrDefaultAsync(new InvestorByShopperSpecification(begun.ShopperId), token).ConfigureAwait(false);
            var investment = investor?.Investments.FirstOrDefault(i => i.PublicId == begun.InvestmentId);
            if (investor is null || investment is null) return false;

            if (result is not null)
            {
                investor.ConfirmInvestmentPlaced(investment, result.OrderId, result.Status);
                _logger.LogInformation("Placed investment {InvestmentId} of {Amount} ({Status}).", investment.PublicId, investment.Amount, investment.Status);
            }
            else
            {
                investor.FailInvestment(investment);
                _logger.LogWarning("Investing the set-aside balance failed ({Error}); it will accrue towards the next investment.", error);
            }
            await repo.SaveChangesAsync(token).ConfigureAwait(false);
            return true;
        }, token).ConfigureAwait(false);
    }

    private async Task ReconcileInvestmentsAsync(CancellationToken token)
    {
        var investors = await ReadAsync(repo => repo.ListAsync(new InvestorsWithUnsettledInvestmentsSpecification(), token), token).ConfigureAwait(false);
        foreach (var snapshot in investors)
        {
            var unsettled = snapshot.Investments
                .Where(i => i.Status == InvestmentStatus.Pending && !string.IsNullOrEmpty(i.UpvestOrderId))
                .ToList();

            foreach (var pendingInvestment in unsettled)
            {
                try
                {
                    var status = await _gateway.GetInvestmentStatusAsync(pendingInvestment.UpvestOrderId!, token).ConfigureAwait(false);
                    if (status == InvestmentStatus.Pending) continue;

                    await MutateAsync(async repo =>
                    {
                        var investor = await repo.FirstOrDefaultAsync(new InvestorByShopperSpecification(snapshot.ShopperId), token).ConfigureAwait(false);
                        var investment = investor?.Investments.FirstOrDefault(i => i.PublicId == pendingInvestment.PublicId);
                        if (investor is null || investment is null || investment.Status != InvestmentStatus.Pending) return false;
                        investor.SettleInvestment(investment, status);
                        await repo.SaveChangesAsync(token).ConfigureAwait(false);
                        _logger.LogInformation("Investment {InvestmentId} is now {Status}.", investment.PublicId, investment.Status);
                        return true;
                    }, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Could not reconcile investment {InvestmentId} ({Error}).", pendingInvestment.PublicId, ex.Message);
                }
            }
        }
    }

    private sealed record BegunInvestment(string ShopperId, string AccountGroupId, string AccountId, Guid InvestmentId, decimal Amount);
}
