using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Default <see cref="IInvestingService"/>. Holds the domain rules for setting
/// aside change and investing it, and leans on <see cref="IInvestmentProvider"/>
/// for everything that happens at Upvest. Keeps no personal details and never
/// logs them.
/// </summary>
public class InvestingService : IInvestingService
{
    // Serialises reconciliation across the process. Webhook callbacks can arrive in bursts, and
    // each request's DbContext writes to the same (in-memory) store; without this, concurrent
    // reconciliations contend. Reconciliation is small and infrequent, so a single gate is fine.
    private static readonly SemaphoreSlim ReconcileGate = new(1, 1);

    private readonly IRepository<Investor> _investors;
    private readonly IInvestmentProvider _provider;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IInvestmentProvider provider,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _provider = provider;
        _logger = logger;
    }

    public async Task<EnrolmentView> EnrolAsync(string buyerId, InvestorRegistration registration, CancellationToken cancellationToken = default)
    {
        var existing = await LoadAsync(buyerId, cancellationToken);
        if (existing is not null)
        {
            // Already opted in — surface the current state rather than enrolling twice.
            await RefreshEnrolmentAsync(existing, cancellationToken);
            return ToEnrolmentView(existing);
        }

        var result = await _provider.EnrolAsync(registration, cancellationToken);

        var investor = new Investor(buyerId, result.ProviderUserId);
        ApplyProviderEnrolment(investor, result);
        await _investors.AddAsync(investor, cancellationToken);

        _logger.LogInformation("Investor enrolled for buyer {BuyerId} with status {Status}.", buyerId, investor.Status);
        return ToEnrolmentView(investor);
    }

    public async Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await LoadAsync(buyerId, cancellationToken);
        if (investor is null)
        {
            return null;
        }

        await RefreshEnrolmentAsync(investor, cancellationToken);
        return ToEnrolmentView(investor);
    }

    public async Task<decimal> ApplyPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken = default)
    {
        // Setting aside change and investing it must never make placing an order fail.
        try
        {
            var investor = await LoadAsync(buyerId, cancellationToken);
            if (investor is null || !investor.CanInvest)
            {
                return 0m;
            }

            var roundUp = SpareChange.RoundUp(orderTotal);
            if (roundUp <= 0m)
            {
                return 0m;
            }

            investor.SetAside(roundUp);
            await _investors.UpdateAsync(investor, cancellationToken);

            await TryInvestAsync(investor, cancellationToken);

            return roundUp;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Setting aside change failed for buyer {BuyerId}; order is unaffected. {Error}", buyerId, ex.Message);
            return 0m;
        }
    }

    public async Task<BalanceView?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await LoadAsync(buyerId, cancellationToken);
        if (investor is null)
        {
            return null;
        }

        await ReconcilePendingAsync(investor, cancellationToken);
        return new BalanceView(investor.PendingAmount, investor.InvestedAmount);
    }

    public async Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await LoadAsync(buyerId, cancellationToken);
        if (investor is null)
        {
            return Array.Empty<InvestmentView>();
        }

        await ReconcilePendingAsync(investor, cancellationToken);

        return investor.Investments
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvestmentView(i.PublicId, i.Amount, i.Status, i.CreatedAt))
            .ToList();
    }

    public async Task ReconcileByProviderOrderAsync(string providerOrderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerOrderId))
        {
            return;
        }

        try
        {
            var investors = await _investors.ListAsync(
                new InvestorsWithInvestmentsSpecification(), cancellationToken);
            var investor = investors.FirstOrDefault(
                i => i.Investments.Any(x => x.ProviderOrderId == providerOrderId));
            if (investor is null)
            {
                return;
            }

            await ReconcilePendingAsync(investor, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Reconciling provider order {OrderId} failed. {Error}", providerOrderId, ex.Message);
        }
    }

    // --- helpers -----------------------------------------------------------

    private Task<Investor?> LoadAsync(string buyerId, CancellationToken ct) =>
        _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), ct);

    /// <summary>Invests the whole set-aside balance when it has reached the threshold.</summary>
    private async Task TryInvestAsync(Investor investor, CancellationToken ct)
    {
        if (!investor.IsReadyToInvest || !investor.CanInvest)
        {
            return;
        }

        var amount = investor.PendingAmount;
        var orderId = await _provider.InvestAsync(investor.ProviderUserId, investor.ProviderAccountId!, amount, ct);

        investor.RecordInvestment(orderId);
        await _investors.UpdateAsync(investor, ct);

        _logger.LogInformation("Invested {Amount} EUR for buyer {BuyerId}.", amount, investor.BuyerId);
    }

    /// <summary>Pulls the latest outcome for every still-pending investment from the provider.</summary>
    private async Task ReconcilePendingAsync(Investor investor, CancellationToken ct)
    {
        var pending = investor.Investments.Where(i => i.Status == InvestmentStatus.Pending).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        await ReconcileGate.WaitAsync(ct);
        try
        {
            var changed = false;
            foreach (var investment in pending)
            {
                try
                {
                    var outcome = await _provider.GetInvestmentOutcomeAsync(investment.ProviderOrderId, ct);
                    switch (outcome)
                    {
                        case ProviderInvestmentOutcome.Settled:
                            investment.Settle();
                            changed = true;
                            break;
                        case ProviderInvestmentOutcome.Failed:
                            investment.Fail();
                            changed = true;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not read outcome for an investment of buyer {BuyerId}. {Error}", investor.BuyerId, ex.Message);
                }
            }

            if (changed)
            {
                await _investors.UpdateAsync(investor, ct);
            }
        }
        finally
        {
            ReconcileGate.Release();
        }
    }

    /// <summary>Re-reads enrolment state from the provider while it is still pending.</summary>
    private async Task RefreshEnrolmentAsync(Investor investor, CancellationToken ct)
    {
        if (investor.Status != EnrolmentStatus.Pending)
        {
            return;
        }

        try
        {
            var result = await _provider.RefreshEnrolmentAsync(investor.ProviderUserId, investor.ProviderAccountId, ct);
            ApplyProviderEnrolment(investor, result);
            await _investors.UpdateAsync(investor, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not refresh enrolment for buyer {BuyerId}. {Error}", investor.BuyerId, ex.Message);
        }
    }

    private static void ApplyProviderEnrolment(Investor investor, ProviderEnrolment result)
    {
        if (!string.IsNullOrEmpty(result.ProviderAccountId))
        {
            investor.SetProviderAccount(result.ProviderAccountId!);
        }

        switch (result.Status)
        {
            case EnrolmentStatus.Active:
                investor.MarkActive();
                break;
            case EnrolmentStatus.Rejected:
                investor.MarkRejected();
                break;
            default:
                investor.MarkPending();
                break;
        }
    }

    private static EnrolmentView ToEnrolmentView(Investor investor) =>
        new(investor.EnrolmentId, investor.Status);
}
