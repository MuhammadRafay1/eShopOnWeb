using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    // A placed order is paid-on-placement in this application (eShopOnWeb has no separate payment step).
    // The shop has no shipping-address capture on this API, so a fixed address is used for the order record.
    private static readonly Address DefaultShipToAddress =
        new("123 Main St.", "Kent", "OH", "United States", "44240");

    // Grace period before a placement whose outcome stayed unknown (no order id ever observed) is written off.
    private static readonly TimeSpan UnknownPlacementGrace = TimeSpan.FromMinutes(10);

    private readonly IRepository<InvestorEnrolment> _enrolments;
    private readonly IRepository<Investment> _investments;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<CatalogItem> _catalogItems;
    private readonly IUriComposer _uriComposer;
    private readonly IUpvestInvestingGateway _gateway;
    private readonly IInvestingLocks _locks;
    private readonly IAppLogger<InvestingService> _logger;
    private readonly TimeProvider _clock;

    public InvestingService(
        IRepository<InvestorEnrolment> enrolments,
        IRepository<Investment> investments,
        IRepository<Order> orders,
        IRepository<CatalogItem> catalogItems,
        IUriComposer uriComposer,
        IUpvestInvestingGateway gateway,
        IInvestingLocks locks,
        IAppLogger<InvestingService> logger,
        TimeProvider clock)
    {
        _enrolments = enrolments;
        _investments = investments;
        _orders = orders;
        _catalogItems = catalogItems;
        _uriComposer = uriComposer;
        _gateway = gateway;
        _locks = locks;
        _logger = logger;
        _clock = clock;
    }

    public async Task<EnrolmentView> EnrolAsync(string buyerId, InvestorSignUp signUp, CancellationToken cancellationToken)
    {
        // Claim: serialize per shopper so a double-submit cannot create two enrolments (the in-memory
        // provider does not enforce the unique BuyerId index, so the lock is the real guard here).
        using var _ = await _locks.AcquireAsync(buyerId, cancellationToken);

        var existing = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerSpecification(buyerId), cancellationToken);
        if (existing is not null)
        {
            return new EnrolmentView(existing.EnrolmentId, existing.Status);
        }

        var now = _clock.GetUtcNow();
        var enrolment = new InvestorEnrolment(buyerId, signUp.Email, signUp.TaxId, signUp.TaxCountry, now);
        enrolment = await _enrolments.AddAsync(enrolment, cancellationToken);

        try
        {
            // Stable idempotency scope keyed to this enrolment, so a retry reuses the same keys.
            var scope = enrolment.EnrolmentId.ToString("N");
            var userId = await _gateway.CreateInvestorAsync(signUp, scope, cancellationToken);
            enrolment.RecordUpvestUser(userId, now);
            await _enrolments.UpdateAsync(enrolment, cancellationToken);
            _logger.LogInformation("Enrolment {EnrolmentId} created an Upvest user; awaiting acceptance.", enrolment.EnrolmentId);

            // Advance as far as possible now (the user usually is not accepted yet, so this stays pending).
            await TryAdvanceOnboardingAsync(enrolment, cancellationToken);
        }
        catch (UpvestGatewayException ex) when (!ex.OutcomeUnknown && ex.StatusCode is >= 400 and < 500)
        {
            // Upvest refused to take the shopper on.
            enrolment.SetStatus(EnrolmentStatus.Rejected, now);
            await _enrolments.UpdateAsync(enrolment, cancellationToken);
            _logger.LogWarning("Enrolment {EnrolmentId} rejected by Upvest (status {StatusCode}).",
                enrolment.EnrolmentId, ex.StatusCode?.ToString() ?? "n/a");
        }
        catch (UpvestGatewayException ex)
        {
            // Transient/unknown outcome: leave pending; a later GET /enrolment reconciles.
            _logger.LogWarning("Enrolment {EnrolmentId} could not be completed at Upvest yet: {Reason}.",
                enrolment.EnrolmentId, ex.Message);
            await _enrolments.UpdateAsync(enrolment, cancellationToken);
        }

        return new EnrolmentView(enrolment.EnrolmentId, enrolment.Status);
    }

    /// <summary>
    /// Advance a pending enrolment: once Upvest has accepted the user, create the account group and account,
    /// and flip to Active once the account is usable. Best-effort — persists whatever progress it makes.
    /// </summary>
    private async Task TryAdvanceOnboardingAsync(InvestorEnrolment enrolment, CancellationToken cancellationToken)
    {
        if (enrolment.Status is EnrolmentStatus.Active or EnrolmentStatus.Rejected) return;
        if (enrolment.UpvestUserId is not Guid userId) return;

        var progress = await _gateway.AdvanceOnboardingAsync(
            userId, enrolment.UpvestAccountGroupId, enrolment.UpvestAccountId,
            enrolment.EnrolmentId.ToString("N"), cancellationToken);

        var now = _clock.GetUtcNow();
        if (progress.AccountGroupId is Guid groupId && enrolment.UpvestAccountGroupId is null)
            enrolment.RecordUpvestAccountGroup(groupId, now);
        if (progress.AccountId is Guid accountId && enrolment.UpvestAccountId is null)
            enrolment.RecordUpvestAccount(accountId, now);
        if (progress.Status != enrolment.Status)
            enrolment.SetStatus(progress.Status, now);

        await _enrolments.UpdateAsync(enrolment, cancellationToken);
        if (progress.Status == EnrolmentStatus.Active)
            _logger.LogInformation("Enrolment {EnrolmentId} is now active; the shopper can invest.", enrolment.EnrolmentId);
    }

    public async Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken)
    {
        var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerSpecification(buyerId), cancellationToken);
        if (enrolment is null) return null;

        // Reconcile acceptance: advance a pending enrolment towards active.
        if (enrolment.Status == EnrolmentStatus.Pending)
        {
            try
            {
                await TryAdvanceOnboardingAsync(enrolment, cancellationToken);
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not reconcile enrolment {EnrolmentId} acceptance: {Reason}.",
                    enrolment.EnrolmentId, ex.Message);
            }
        }

        return new EnrolmentView(enrolment.EnrolmentId, enrolment.Status);
    }

    public async Task<OrderPlacementResult> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken)
    {
        // 1) Create the order from catalog items, reusing the existing Order/OrderItem model. This is the
        //    point the order becomes "paid" in this application; it must succeed regardless of investing.
        var catalogItemIds = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItems.ListAsync(new CatalogItemsSpecification(catalogItemIds), cancellationToken);

        var items = new List<OrderItem>();
        foreach (var line in lines)
        {
            var catalogItem = catalogItems.FirstOrDefault(c => c.Id == line.CatalogItemId)
                ?? throw new ArgumentException($"Catalog item {line.CatalogItemId} does not exist.");
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            items.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        var order = new Order(buyerId, DefaultShipToAddress, items);
        order = await _orders.AddAsync(order, cancellationToken);

        // 2) Setting aside the change — never let anything here fail the order.
        decimal roundUp = 0m;
        try
        {
            roundUp = await SetAsideAndMaybeInvestAsync(buyerId, order.Total(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Order {OrderId} placed; setting aside change failed and was ignored: {Reason}.",
                order.Id, ex.Message);
            roundUp = 0m;
        }

        return new OrderPlacementResult(order.Id, roundUp);
    }

    private async Task<decimal> SetAsideAndMaybeInvestAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken)
    {
        var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerSpecification(buyerId), cancellationToken);
        if (enrolment is null) return 0m;

        // Nudge a pending enrolment towards active so a shopper who has been accepted since enrolling starts
        // setting aside without needing a separate GET /enrolment call. Best-effort; never fails the order.
        if (enrolment.Status == EnrolmentStatus.Pending)
        {
            try { await TryAdvanceOnboardingAsync(enrolment, cancellationToken); }
            catch (UpvestGatewayException) { /* reflected on the next read */ }
        }

        // Orders from a shopper who is not an accepted investor set nothing aside.
        if (!enrolment.CanInvest)
        {
            return 0m;
        }

        var roundUp = decimal.Round(Math.Ceiling(orderTotal) - orderTotal, 2, MidpointRounding.AwayFromZero);
        if (roundUp <= 0m)
        {
            return 0m;
        }

        // Serialize per shopper: set aside and (if the threshold is crossed) invest, atomically.
        using var _ = await _locks.AcquireAsync(buyerId, cancellationToken);

        // Re-read inside the lock so the balance decision is made on current state.
        enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerSpecification(buyerId), cancellationToken);
        if (enrolment is null || !enrolment.CanInvest) return 0m;

        var now = _clock.GetUtcNow();
        enrolment.AddSetAside(roundUp, now);
        await _enrolments.UpdateAsync(enrolment, cancellationToken);
        _logger.LogInformation("Set aside {RoundUp} for shopper; balance now {Balance}.", roundUp, enrolment.SetAsideBalance);

        if (enrolment.SetAsideBalance >= InvestingPolicy.InvestmentThresholdEuros)
        {
            await InvestWholeBalanceAsync(enrolment, cancellationToken);
        }

        return roundUp;
    }

    private async Task InvestWholeBalanceAsync(InvestorEnrolment enrolment, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();

        // Claim: take the balance and record a pending investment BEFORE calling Upvest.
        var amount = enrolment.TakeBalanceForInvestment(now);
        await _enrolments.UpdateAsync(enrolment, cancellationToken);

        var investment = new Investment(enrolment.BuyerId, amount, now);
        investment = await _investments.AddAsync(investment, cancellationToken);

        try
        {
            var result = await _gateway.PlaceInvestmentAsync(
                enrolment.UpvestUserId!.Value,
                enrolment.UpvestAccountGroupId!.Value,
                enrolment.UpvestAccountId!.Value,
                amount,
                clientReference: investment.InvestmentId.ToString("N"),
                idempotencyScope: investment.InvestmentId.ToString("N"),
                cancellationToken);

            investment.RecordUpvestOrder(result.OrderId, now);
            investment.SetStatus(result.Status, now);
            await _investments.UpdateAsync(investment, cancellationToken);
            _logger.LogInformation("Invested {Amount} for shopper; Upvest order {OrderId} status {Status}.",
                amount, result.OrderId, result.Status);
        }
        catch (UpvestGatewayException ex) when (ex.OutcomeUnknown)
        {
            // The placement may or may not have reached Upvest — reconcile by the client reference.
            _logger.LogWarning("Investment {InvestmentId} outcome unknown; reconciling.", investment.InvestmentId);
            try
            {
                var found = await _gateway.FindInvestmentByReferenceAsync(
                    enrolment.UpvestAccountId!.Value, investment.InvestmentId.ToString("N"), cancellationToken);
                if (found is not null)
                {
                    investment.RecordUpvestOrder(found.OrderId, _clock.GetUtcNow());
                    investment.SetStatus(found.Status, _clock.GetUtcNow());
                    await _investments.UpdateAsync(investment, cancellationToken);
                }
                // else: leave pending with no order id; GetInvestments/GetBalance will reconcile later.
            }
            catch (UpvestGatewayException reconcileEx)
            {
                _logger.LogWarning("Investment {InvestmentId} reconciliation deferred: {Reason}.",
                    investment.InvestmentId, reconcileEx.Message);
            }
        }
        catch (UpvestGatewayException ex)
        {
            // Definite failure: the placement did not happen. Return the money to the set-aside balance.
            _logger.LogWarning("Investment {InvestmentId} failed at Upvest (status {StatusCode}); returning {Amount} to balance.",
                investment.InvestmentId, ex.StatusCode?.ToString() ?? "n/a", amount);
            investment.SetStatus(InvestmentStatus.Failed, now);
            await _investments.UpdateAsync(investment, cancellationToken);

            var fresh = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerSpecification(enrolment.BuyerId), cancellationToken);
            if (fresh is not null)
            {
                fresh.ReturnFailedInvestment(amount, _clock.GetUtcNow());
                await _enrolments.UpdateAsync(fresh, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken)
    {
        await ReconcilePendingInvestmentsAsync(buyerId, cancellationToken);

        var investments = await _investments.ListAsync(new InvestmentsByBuyerSpecification(buyerId), cancellationToken);
        return investments
            .Select(i => new InvestmentView(i.InvestmentId, i.Amount, i.Status, i.CreatedAt))
            .ToList();
    }

    public async Task<BalanceView> GetBalanceAsync(string buyerId, CancellationToken cancellationToken)
    {
        await ReconcilePendingInvestmentsAsync(buyerId, cancellationToken);

        var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerSpecification(buyerId), cancellationToken);
        var pending = enrolment?.SetAsideBalance ?? 0m;

        var investments = await _investments.ListAsync(new InvestmentsByBuyerSpecification(buyerId), cancellationToken);
        // "Invested so far" is everything placed and not failed (failed amounts are returned to the balance).
        var invested = investments
            .Where(i => i.Status != InvestmentStatus.Failed)
            .Sum(i => i.Amount);

        return new BalanceView(decimal.Round(pending, 2, MidpointRounding.AwayFromZero),
                               decimal.Round(invested, 2, MidpointRounding.AwayFromZero));
    }

    private async Task ReconcilePendingInvestmentsAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investments = await _investments.ListAsync(new InvestmentsByBuyerSpecification(buyerId), cancellationToken);
        var pending = investments.Where(i => i.Status == InvestmentStatus.Pending).ToList();
        if (pending.Count == 0) return;

        InvestorEnrolment? enrolment = null;
        var now = _clock.GetUtcNow();

        foreach (var investment in pending)
        {
            try
            {
                if (investment.UpvestOrderId is Guid orderId)
                {
                    var status = await _gateway.GetInvestmentOutcomeAsync(orderId, cancellationToken);
                    if (status != InvestmentStatus.Pending)
                    {
                        investment.SetStatus(status, now);
                        await _investments.UpdateAsync(investment, cancellationToken);
                        if (status == InvestmentStatus.Failed)
                        {
                            enrolment ??= await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerSpecification(buyerId), cancellationToken);
                            if (enrolment is not null)
                            {
                                enrolment.ReturnFailedInvestment(investment.Amount, now);
                                await _enrolments.UpdateAsync(enrolment, cancellationToken);
                            }
                        }
                    }
                }
                else
                {
                    // Placement whose outcome was unknown and never observed: find it by reference.
                    enrolment ??= await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerSpecification(buyerId), cancellationToken);
                    if (enrolment?.UpvestAccountId is Guid accountId)
                    {
                        var found = await _gateway.FindInvestmentByReferenceAsync(accountId, investment.InvestmentId.ToString("N"), cancellationToken);
                        if (found is not null)
                        {
                            investment.RecordUpvestOrder(found.OrderId, now);
                            investment.SetStatus(found.Status, now);
                            await _investments.UpdateAsync(investment, cancellationToken);
                        }
                        else if (now - investment.CreatedAt > UnknownPlacementGrace)
                        {
                            // The placement never landed. Write it off and return the money to the balance.
                            investment.SetStatus(InvestmentStatus.Failed, now);
                            await _investments.UpdateAsync(investment, cancellationToken);
                            enrolment.ReturnFailedInvestment(investment.Amount, now);
                            await _enrolments.UpdateAsync(enrolment, cancellationToken);
                        }
                    }
                }
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not reconcile investment {InvestmentId}: {Reason}.",
                    investment.InvestmentId, ex.Message);
            }
        }
    }
}
