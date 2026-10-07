using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates the investing flows. The set-aside ledger lives locally and is updated transactionally;
/// everything that touches Upvest (enrolment status, accounts, funding, orders, settlement) is treated
/// as best-effort from the order path so that placing an order can never fail because of investing.
///
/// Investing is driven only by the reconciler — never inline on the order path and never from a webhook
/// — so an order is never delayed by the provider and provider events can never feed back into new
/// orders. All mutations of one shopper's record are serialized by a per-shopper lock, since the
/// in-memory store has no optimistic-concurrency token to catch a lost update.
///
/// Logs carry only non-personal references (Upvest ids, amounts) — never the shopper's sign-up details.
/// </summary>
public class InvestingService : IInvestingService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    private readonly IRepository<Investor> _investors;
    private readonly IInvestingGateway _gateway;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IInvestingGateway gateway,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<EnrolmentView> EnrolAsync(string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(details, nameof(details));

        var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (existing is not null)
        {
            return new EnrolmentView(existing.Id, existing.Status);
        }

        // The create call to Upvest happens outside the lock; the lock only guards the local write and
        // the idempotency check, so two concurrent enrolments cannot both create a record.
        var result = await _gateway.CreateInvestorAsync(details, cancellationToken);

        return await WithLockAsync(buyerId, async () =>
        {
            var again = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
            if (again is not null)
            {
                return new EnrolmentView(again.Id, again.Status);
            }

            var investor = new Investor(buyerId, result.UpvestUserId, result.Status);
            await _investors.AddAsync(investor, cancellationToken);
            _logger.LogInformation("Investor enrolled (enrolmentId {0}, upvestUser {1}, status {2}).",
                investor.Id, investor.UpvestUserId, investor.Status);
            return new EnrolmentView(investor.Id, investor.Status);
        });
    }

    public async Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        return await WithLockAsync(buyerId, async () =>
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
            if (investor is null)
            {
                return (EnrolmentView?)null;
            }

            if (investor.Status == EnrolmentStatus.Pending && await RefreshEnrolmentStatusAsync(investor, cancellationToken))
            {
                await _investors.UpdateAsync(investor, cancellationToken);
            }
            return new EnrolmentView(investor.Id, investor.Status);
        });
    }

    public async Task<decimal> RecordPaidOrderAsync(string buyerId, decimal orderTotalEur, CancellationToken cancellationToken = default)
    {
        try
        {
            return await WithLockAsync(buyerId, async () =>
            {
                var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);

                // Only an accepted investor sets anything aside. The enrolment's local status is kept
                // current by the reconciler, so the order path stays fast and makes no network calls.
                if (investor is null || investor.Status != EnrolmentStatus.Active)
                {
                    return 0m;
                }

                var roundUpCents = RoundUpCents(orderTotalEur);
                if (roundUpCents == 0)
                {
                    return 0m;
                }

                investor.SetAside(roundUpCents);
                await _investors.UpdateAsync(investor, cancellationToken);

                // Investing itself is driven by the reconciler, not inline, so a slow or failing provider
                // can never delay or fail order placement.
                return roundUpCents / 100m;
            });
        }
        catch (Exception ex)
        {
            // Investing must never break order placement.
            _logger.LogWarning("Setting aside change for a paid order failed: {0}", ex.Message);
            return 0m;
        }
    }

    public async Task<BalanceView?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        return await WithLockAsync(buyerId, async () =>
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
            if (investor is null)
            {
                return (BalanceView?)null;
            }

            if (await RefreshInvestmentsStatusAsync(investor, cancellationToken))
            {
                await _investors.UpdateAsync(investor, cancellationToken);
            }
            return new BalanceView(investor.PendingAmount, investor.InvestedAmount);
        });
    }

    public async Task<InvestmentsView> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        return await WithLockAsync(buyerId, async () =>
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
            if (investor is null)
            {
                return new InvestmentsView(Array.Empty<InvestmentView>());
            }

            if (await RefreshInvestmentsStatusAsync(investor, cancellationToken))
            {
                await _investors.UpdateAsync(investor, cancellationToken);
            }

            var views = investor.Investments
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.Id)
                .Select(i => new InvestmentView(i.Id, i.Amount, i.Status))
                .ToList();
            return new InvestmentsView(views);
        });
    }

    public Task ReconcileAsync(CancellationToken cancellationToken = default)
        => ForEachInvestorAsync(invest: true, cancellationToken);

    public Task SettlePendingAsync(CancellationToken cancellationToken = default)
        => ForEachInvestorAsync(invest: false, cancellationToken);

    /// <summary>
    /// Walk every investor: advance a pending enrolment, optionally invest a ready balance, and settle
    /// pending investments. Investing is only ever done here, under the per-shopper lock.
    /// </summary>
    private async Task ForEachInvestorAsync(bool invest, CancellationToken cancellationToken)
    {
        var investors = await _investors.ListAsync(new InvestorsWithInvestmentsSpecification(), cancellationToken);
        foreach (var snapshot in investors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await WithLockAsync(snapshot.BuyerId, async () =>
                {
                    // Re-load inside the lock so we act on the latest state.
                    var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(snapshot.BuyerId), cancellationToken);
                    if (investor is null)
                    {
                        return true;
                    }

                    var changed = false;
                    if (investor.Status == EnrolmentStatus.Pending)
                    {
                        changed |= await RefreshEnrolmentStatusAsync(investor, cancellationToken);
                    }

                    if (invest && investor.CanInvest)
                    {
                        await InvestAsync(investor, cancellationToken);
                        return true; // InvestAsync persists its own changes.
                    }

                    changed |= await RefreshInvestmentsStatusAsync(investor, cancellationToken);
                    if (changed)
                    {
                        await _investors.UpdateAsync(investor, cancellationToken);
                    }
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Reconciling investor {0} failed: {1}", snapshot.Id, ex.Message);
            }
        }
    }

    // --- helpers -----------------------------------------------------------------------------

    /// <summary>Round-up to the next whole euro, in cents. 0 when the total is already whole euros.</summary>
    private static long RoundUpCents(decimal orderTotalEur)
    {
        var totalCents = (long)Math.Round(orderTotalEur * 100m, MidpointRounding.AwayFromZero);
        if (totalCents <= 0)
        {
            return 0;
        }
        return (100 - (totalCents % 100)) % 100;
    }

    private async Task<bool> RefreshEnrolmentStatusAsync(Investor investor, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _gateway.GetEnrolmentStatusAsync(investor.UpvestUserId, cancellationToken);
            if (status != investor.Status)
            {
                investor.UpdateEnrolmentStatus(status);
                _logger.LogInformation("Enrolment {0} moved to {1}.", investor.Id, status);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Refreshing enrolment {0} failed: {1}", investor.Id, ex.Message);
        }
        return false;
    }

    private async Task<bool> RefreshInvestmentsStatusAsync(Investor investor, CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var investment in investor.Investments.Where(i => i.Status == InvestmentStatus.Pending).ToList())
        {
            try
            {
                var status = await _gateway.GetInvestmentStatusAsync(investment.UpvestOrderId, cancellationToken);
                if (status != InvestmentStatus.Pending)
                {
                    investor.ApplyInvestmentOutcome(investment, status);
                    _logger.LogInformation("Investment {0} (order {1}) settled as {2}.",
                        investment.Id, investment.UpvestOrderId, status);
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Refreshing investment {0} failed: {1}", investment.Id, ex.Message);
            }
        }
        return changed;
    }

    private async Task InvestAsync(Investor investor, CancellationToken cancellationToken)
    {
        try
        {
            if (!investor.CanInvest)
            {
                return;
            }

            if (!investor.HasUpvestAccount)
            {
                var account = await _gateway.EnsureAccountAsync(investor.UpvestUserId, cancellationToken);
                investor.SetUpvestAccount(account.AccountId, account.AccountGroupId);
                await _investors.UpdateAsync(investor, cancellationToken);
            }

            var amountEur = investor.PendingAmount;

            // Forward the set-aside cash to the shopper's Upvest account, then invest it.
            await _gateway.AddCashAsync(investor.UpvestAccountGroupId!.Value, amountEur, cancellationToken);
            var result = await _gateway.PlaceInvestmentOrderAsync(
                investor.UpvestUserId, investor.UpvestAccountId!.Value, amountEur, cancellationToken);

            var investment = investor.StartInvestment(result.UpvestOrderId, result.Status);
            await _investors.UpdateAsync(investor, cancellationToken);

            _logger.LogInformation("Invested {0:0.00} for investor {1} as order {2} (status {3}).",
                amountEur, investor.Id, result.UpvestOrderId, result.Status);
        }
        catch (Exception ex)
        {
            // Leave the balance set aside; the reconciler will retry on the next tick.
            _logger.LogWarning("Investing the set-aside balance for investor {0} failed: {1}", investor.Id, ex.Message);
        }
    }

    private static async Task<T> WithLockAsync<T>(string buyerId, Func<Task<T>> action)
    {
        var gate = Locks.GetOrAdd(buyerId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
    }
}
