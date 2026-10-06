using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Scoped reconciler: settles each pending investment's status against the provider (Flow 4) and advances
/// pending enrolments. A pending investment with no order id (placement outcome unknown) is resolved by its
/// client reference after a grace period, so this never races an in-flight placement. Authoritative status
/// always comes from a (signed) provider read — inbound webhooks only nudge this to run.
/// </summary>
public sealed class InvestmentReconciler : IInvestmentReconciler
{
    private static readonly TimeSpan UnknownGrace = TimeSpan.FromSeconds(60);

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestInvestorGateway _gateway;
    private readonly ILogger<InvestmentReconciler> _logger;

    public InvestmentReconciler(
        IRepository<Investor> investors,
        IRepository<Investment> investments,
        IUpvestInvestorGateway gateway,
        ILogger<InvestmentReconciler> logger)
    {
        _investors = investors;
        _investments = investments;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task ReconcileAllAsync(CancellationToken cancellationToken)
    {
        foreach (var investment in await _investments.ListAsync(new PendingInvestmentsSpec(), cancellationToken))
        {
            try
            {
                await ReconcileInvestmentAsync(investment, cancellationToken);
            }
            catch (UpvestProviderException ex)
            {
                _logger.LogWarning("Could not reconcile investment {Id}: {Message}", investment.Id, ex.Message);
            }
        }

        foreach (var investor in await _investors.ListAsync(new PendingEnrolmentsSpec(), cancellationToken))
        {
            if (investor.UpvestAccountId is not { } accountId) continue;
            try
            {
                var status = await _gateway.GetEnrolmentStatusAsync(accountId, cancellationToken);
                if (status != investor.Status)
                {
                    investor.SetStatus(status);
                    await _investors.UpdateAsync(investor, cancellationToken);
                    _logger.LogInformation("Enrolment {Id} reconciled to {Status}.", investor.Id, status);
                }
            }
            catch (UpvestProviderException ex)
            {
                _logger.LogWarning("Could not reconcile enrolment {Id}: {Message}", investor.Id, ex.Message);
            }
        }
    }

    private async Task ReconcileInvestmentAsync(Investment investment, CancellationToken ct)
    {
        if (investment.UpvestOrderId is { } orderId)
        {
            await ApplyAsync(investment, await _gateway.GetOrderStatusAsync(orderId, ct), ct);
            return;
        }

        if (DateTimeOffset.UtcNow - investment.CreatedAt < UnknownGrace) return;

        var investor = await _investors.GetByIdAsync(investment.InvestorId, ct);
        if (investor?.UpvestAccountId is not { } accountId) return;

        var found = await _gateway.FindOrderByReferenceAsync(accountId, investment.Reference, ct);
        if (found is null)
        {
            investor.Refund(investment.AmountCents);
            await _investors.UpdateAsync(investor, ct);
            await _investments.DeleteAsync(investment, ct);
            _logger.LogInformation("Investment {Id} never placed; refunded {Cents}c.", investment.Id, investment.AmountCents);
            return;
        }

        investment.AttachOrder(found.OrderId);
        await ApplyAsync(investment, found.Status, ct);
    }

    private async Task ApplyAsync(Investment investment, InvestmentStatus status, CancellationToken ct)
    {
        switch (status)
        {
            case InvestmentStatus.Settled:
                investment.MarkSettled();
                await _investments.UpdateAsync(investment, ct);
                _logger.LogInformation("Investment {Id} settled.", investment.Id);
                break;

            case InvestmentStatus.Failed:
                investment.MarkFailed();
                await _investments.UpdateAsync(investment, ct);
                var investor = await _investors.GetByIdAsync(investment.InvestorId, ct);
                if (investor is not null)
                {
                    investor.Refund(investment.AmountCents);
                    await _investors.UpdateAsync(investor, ct);
                }
                _logger.LogInformation("Investment {Id} failed; refunded {Cents}c.", investment.Id, investment.AmountCents);
                break;
        }
    }
}
