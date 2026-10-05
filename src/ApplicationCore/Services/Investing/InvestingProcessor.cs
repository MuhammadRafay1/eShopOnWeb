using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Investing;

/// <summary>
/// Drives investing forward off the request path. Every pass is idempotent: provider writes use the stable
/// idempotency keys stored on the aggregates, so re-attempting an unfinished step never duplicates at the
/// provider. Failures on one shopper never stop the others.
/// </summary>
public class InvestingProcessor : IInvestingProcessor
{
    private readonly IRepository<Enrolment> _enrolments;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestInvestorGateway _gateway;
    private readonly IShopperConcurrencyGuard _guard;
    private readonly IAppLogger<InvestingProcessor> _logger;

    public InvestingProcessor(
        IRepository<Enrolment> enrolments,
        IRepository<Investment> investments,
        IUpvestInvestorGateway gateway,
        IShopperConcurrencyGuard guard,
        IAppLogger<InvestingProcessor> logger)
    {
        _enrolments = enrolments;
        _investments = investments;
        _gateway = gateway;
        _guard = guard;
        _logger = logger;
    }

    public async Task ProcessEnrolmentsAsync(CancellationToken cancellationToken)
    {
        var pending = await _enrolments.ListAsync(new EnrolmentsByStatusSpecification(EnrolmentStatus.Pending), cancellationToken);
        foreach (var enrolment in pending)
        {
            if (cancellationToken.IsCancellationRequested) return;
            if (enrolment.UpvestUserId is not Guid userId || !enrolment.OnboardingSubmitted) continue; // needs the sign-up form (re-POST)

            try
            {
                var status = await _gateway.GetInvestorStatusAsync(userId, cancellationToken);
                if (status == ProviderInvestorStatus.Active)
                {
                    enrolment.MarkActive();
                    await _enrolments.UpdateAsync(enrolment, cancellationToken);
                    _logger.LogInformation("Enrolment {0} accepted by the provider.", enrolment.Id);
                }
                else if (status == ProviderInvestorStatus.Rejected)
                {
                    enrolment.MarkRejected();
                    await _enrolments.UpdateAsync(enrolment, cancellationToken);
                    _logger.LogInformation("Enrolment {0} rejected by the provider.", enrolment.Id);
                }
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning($"Polling investor status for enrolment {enrolment.Id} failed (will retry): {ex.Message}");
            }
        }

        // Provision accounts for any accepted-but-unprovisioned enrolment.
        var active = await _enrolments.ListAsync(new EnrolmentsByStatusSpecification(EnrolmentStatus.Active), cancellationToken);
        foreach (var enrolment in active.Where(e => !e.AccountsProvisioned && e.UpvestUserId is not null))
        {
            if (cancellationToken.IsCancellationRequested) return;
            try
            {
                var userId = enrolment.UpvestUserId!.Value;
                var groupId = enrolment.AccountGroupId ?? await _gateway.CreateAccountGroupAsync(userId, enrolment.AccountGroupIdempotencyKey, cancellationToken);
                var accountId = await _gateway.CreateAccountAsync(userId, groupId, enrolment.AccountIdempotencyKey, cancellationToken);
                enrolment.SetAccounts(groupId, accountId);
                await _enrolments.UpdateAsync(enrolment, cancellationToken);
                _logger.LogInformation("Enrolment {0} provisioned account group and account.", enrolment.Id);
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning($"Provisioning accounts for enrolment {enrolment.Id} failed (will retry): {ex.Message}");
            }
        }
    }

    public async Task ProcessInvestmentsAsync(CancellationToken cancellationToken)
    {
        // 1. Trigger a new investment for every ready enrolment whose balance has reached the threshold.
        var active = await _enrolments.ListAsync(new EnrolmentsByStatusSpecification(EnrolmentStatus.Active), cancellationToken);
        foreach (var enrolment in active.Where(e => e.AccountsProvisioned && e.PendingAmount >= InvestingConstants.InvestmentThreshold))
        {
            if (cancellationToken.IsCancellationRequested) return;
            using var _ = await _guard.LockAsync(enrolment.ShopperId, cancellationToken);
            var fresh = await _enrolments.GetByIdAsync(enrolment.Id, cancellationToken);
            if (fresh is null || fresh.Status != EnrolmentStatus.Active || !fresh.AccountsProvisioned) continue;
            if (!fresh.TryTakeForInvestment(InvestingConstants.InvestmentThreshold, out var amount)) continue;

            var investment = new Investment(fresh.ShopperId, amount);
            await _investments.AddAsync(investment, cancellationToken);
            await _enrolments.UpdateAsync(fresh, cancellationToken);
            _logger.LogInformation("Investment {0} created for {1} (enrolment {2}).", investment.Id, amount, fresh.Id);
        }

        // 2. Carry each not-yet-placed pending investment forward: fund, then place the order (both idempotent).
        var pending = await _investments.ListAsync(new InvestmentsByStatusSpecification(InvestmentStatus.Pending), cancellationToken);
        foreach (var investment in pending.Where(i => i.UpvestOrderId is null))
        {
            if (cancellationToken.IsCancellationRequested) return;
            var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(investment.ShopperId), cancellationToken);
            if (enrolment?.AccountGroupId is not Guid groupId || enrolment.AccountId is not Guid accountId) continue;

            try
            {
                if (!await _gateway.IsAccountActiveAsync(accountId, cancellationToken)) continue; // not tradeable yet

                investment.RecordAttempt();
                if (investment.Attempts > InvestingConstants.MaxInvestmentAttempts)
                {
                    await FailAndRefundAsync(investment, enrolment.ShopperId, cancellationToken);
                    continue;
                }

                if (!investment.ToppedUp)
                {
                    await _gateway.TopUpAsync(groupId, investment.Amount, investment.TopUpIdempotencyKey, cancellationToken);
                    investment.MarkToppedUp();
                    await _investments.UpdateAsync(investment, cancellationToken);
                }

                var orderId = await _gateway.PlaceInvestmentOrderAsync(accountId, investment.Amount, investment.OrderIdempotencyKey, cancellationToken);
                investment.SetOrderPlaced(orderId);
                await _investments.UpdateAsync(investment, cancellationToken);
                _logger.LogInformation("Investment {0} placed as provider order {1}.", investment.Id, orderId);
            }
            catch (UpvestGatewayException ex) when (ex.OutcomeUnknown)
            {
                // The write may have landed; leave pending and retry with the same idempotency key next pass.
                await _investments.UpdateAsync(investment, cancellationToken);
                _logger.LogWarning($"Investment {investment.Id} provider call outcome unknown; will reconcile on retry: {ex.Message}");
            }
            catch (UpvestGatewayException ex)
            {
                await _investments.UpdateAsync(investment, cancellationToken);
                _logger.LogWarning($"Investment {investment.Id} provider call failed (attempt {investment.Attempts}): {ex.Message}");
                if (investment.Attempts >= InvestingConstants.MaxInvestmentAttempts)
                    await FailAndRefundAsync(investment, enrolment.ShopperId, cancellationToken);
            }
        }
    }

    public async Task ReconcileInvestmentsAsync(CancellationToken cancellationToken)
    {
        var pending = await _investments.ListAsync(new InvestmentsByStatusSpecification(InvestmentStatus.Pending), cancellationToken);
        foreach (var investment in pending.Where(i => i.UpvestOrderId is not null))
        {
            if (cancellationToken.IsCancellationRequested) return;
            try
            {
                var outcome = await _gateway.GetOrderOutcomeAsync(investment.UpvestOrderId!.Value, cancellationToken);
                if (outcome == ProviderOrderOutcome.Settled)
                {
                    investment.MarkSettled();
                    await _investments.UpdateAsync(investment, cancellationToken);
                    _logger.LogInformation("Investment {0} settled.", investment.Id);
                }
                else if (outcome == ProviderOrderOutcome.Failed)
                {
                    await FailAndRefundAsync(investment, investment.ShopperId, cancellationToken);
                    _logger.LogInformation("Investment {0} failed at the provider; set-aside returned to balance.", investment.Id);
                }
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning($"Reconciling investment {investment.Id} failed (will retry): {ex.Message}");
            }
        }
    }

    private async Task FailAndRefundAsync(Investment investment, string shopperId, CancellationToken cancellationToken)
    {
        using var _ = await _guard.LockAsync(shopperId, cancellationToken);
        var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(shopperId), cancellationToken);
        if (enrolment is not null)
        {
            enrolment.ReturnToBalance(investment.Amount);
            await _enrolments.UpdateAsync(enrolment, cancellationToken);
        }
        investment.MarkFailed();
        await _investments.UpdateAsync(investment, cancellationToken);
    }
}
