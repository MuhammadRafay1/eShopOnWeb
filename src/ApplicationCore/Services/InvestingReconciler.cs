using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Advances every pending enrolment and investment by reading the authoritative state at Upvest and
/// taking the next step. This is the source of truth for status (Flow 4): statuses reflect what actually
/// happened at Upvest, whether or not a webhook arrived. Safe to run repeatedly and idempotently.
/// </summary>
public class InvestingReconciler
{
    private readonly IRepository<Enrolment> _enrolments;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestGateway _upvest;
    private readonly IAppLogger<InvestingReconciler> _logger;

    public InvestingReconciler(
        IRepository<Enrolment> enrolments,
        IRepository<Investment> investments,
        IUpvestGateway upvest,
        IAppLogger<InvestingReconciler> logger)
    {
        _enrolments = enrolments;
        _investments = investments;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        foreach (var enrolment in await _enrolments.ListAsync(new IncompleteEnrolmentsSpecification(), ct))
        {
            ct.ThrowIfCancellationRequested();
            try { await AdvanceEnrolmentAsync(enrolment, ct); }
            catch (UpvestGatewayException ex) when (ex.IsTransient)
            {
                enrolment.RecordTransientError(ex.Message);
                await _enrolments.UpdateAsync(enrolment, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Enrolment {enrolment.PublicId} reconcile error: {ex.GetType().Name}.");
            }
        }

        foreach (var investment in await _investments.ListAsync(new IncompleteInvestmentsSpecification(), ct))
        {
            ct.ThrowIfCancellationRequested();
            try { await AdvanceInvestmentAsync(investment, ct); }
            catch (UpvestGatewayException ex) when (ex.IsTransient)
            {
                investment.RecordTransientError(ex.Message);
                await _investments.UpdateAsync(investment, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Investment {investment.Id} reconcile error: {ex.GetType().Name}.");
            }
        }
    }

    private async Task AdvanceEnrolmentAsync(Enrolment enrolment, CancellationToken ct)
    {
        switch (enrolment.Stage)
        {
            case EnrolmentStage.AwaitingUserActivation:
                if (enrolment.UpvestUserId is not { } userId)
                    return; // creation never completed; a re-submitted opt-in will carry the details again
                var userState = await _upvest.GetUserStateAsync(userId, ct);
                if (userState == UpvestUserState.Rejected)
                {
                    enrolment.MarkRejected("Upvest rejected the user.");
                    await _enrolments.UpdateAsync(enrolment, ct);
                }
                else if (userState == UpvestUserState.Active)
                {
                    try
                    {
                        var groupId = await _upvest.CreateAccountGroupAsync(userId, enrolment.CreateAccountGroupIdempotencyKey, ct);
                        var accountId = await _upvest.CreateTradingAccountAsync(userId, groupId, enrolment.CreateAccountIdempotencyKey, ct);
                        enrolment.RecordAccounts(groupId, accountId);
                        await _enrolments.UpdateAsync(enrolment, ct);
                        _logger.LogInformation($"Enrolment {enrolment.PublicId} user active; accounts created, awaiting account activation.");
                    }
                    catch (UpvestGatewayException ex) when (!ex.IsTransient)
                    {
                        enrolment.MarkRejected($"Account creation rejected (status {ex.StatusCode}).");
                        await _enrolments.UpdateAsync(enrolment, ct);
                    }
                }
                break;

            case EnrolmentStage.AwaitingAccountActivation:
                if (enrolment.UpvestAccountId is not { } accountToCheck)
                    return;
                var accountState = await _upvest.GetAccountStateAsync(accountToCheck, ct);
                if (accountState == UpvestAccountState.Active)
                {
                    enrolment.MarkActive();
                    await _enrolments.UpdateAsync(enrolment, ct);
                    _logger.LogInformation($"Enrolment {enrolment.PublicId} is now active.");
                }
                else if (accountState == UpvestAccountState.Rejected)
                {
                    enrolment.MarkRejected("Upvest rejected the trading account.");
                    await _enrolments.UpdateAsync(enrolment, ct);
                }
                break;
        }
    }

    private async Task AdvanceInvestmentAsync(Investment investment, CancellationToken ct)
    {
        var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(investment.ShopperId), ct);
        if (enrolment?.UpvestAccountGroupId is not { } groupId || enrolment.UpvestAccountId is not { } accountId)
            return; // enrolment not ready; try again next pass

        switch (investment.Stage)
        {
            case InvestmentStage.Funding:
                var topupId = await _upvest.CreateTopupAsync(groupId, investment.AmountCents, investment.TopupIdempotencyKey, ct);
                investment.RecordTopup(topupId);
                investment.MarkFunded();
                await _investments.UpdateAsync(investment, ct);
                break;

            case InvestmentStage.AwaitingFunds:
                // The account group's available cash confirms the top-up landed (the mock credits it a few
                // seconds after the top-up is created).
                var availableCents = await _upvest.GetAvailableCashCentsAsync(groupId, ct);
                if (availableCents >= investment.AmountCents)
                {
                    investment.MarkPlacing();
                    await _investments.UpdateAsync(investment, ct);
                    goto case InvestmentStage.Placing; // place immediately now that funds are available
                }
                break;

            case InvestmentStage.Placing:
                var orderId = await _upvest.PlaceBuyOrderAsync(accountId, investment.AmountCents, investment.Id.ToString(), investment.OrderIdempotencyKey, ct);
                investment.RecordOrder(orderId);
                await _investments.UpdateAsync(investment, ct);
                _logger.LogInformation($"Investment {investment.Id} order placed, awaiting fill.");
                break;

            case InvestmentStage.AwaitingFill:
                if (investment.UpvestOrderId is not { } order) { investment.MarkFailed("No order id recorded."); await FailAndReturn(investment, enrolment, ct); return; }
                var orderState = await _upvest.GetOrderStateAsync(order, ct);
                if (orderState == UpvestOrderState.Filled)
                {
                    investment.MarkSettled();
                    await _investments.UpdateAsync(investment, ct);
                    _logger.LogInformation($"Investment {investment.Id} settled.");
                }
                else if (orderState == UpvestOrderState.Cancelled)
                {
                    investment.MarkFailed("Order cancelled at Upvest.");
                    await FailAndReturn(investment, enrolment, ct);
                }
                break;
        }
    }

    // A failed investment returns its amount to the set-aside balance so no spare change is lost.
    private async Task FailAndReturn(Investment investment, Enrolment enrolment, CancellationToken ct)
    {
        await _investments.UpdateAsync(investment, ct);
        enrolment.ReturnToPending(investment.AmountCents);
        await _enrolments.UpdateAsync(enrolment, ct);
        _logger.LogWarning($"Investment {investment.Id} failed; {investment.AmountCents} cents returned to set-aside balance.");
    }
}
