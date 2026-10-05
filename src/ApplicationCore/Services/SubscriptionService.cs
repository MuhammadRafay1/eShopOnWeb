using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Subscribe flow: validate the plan against the billing catalog → claim (buyer, plan) locally → ensure a billing
/// customer exists → create the subscription → record it. The billing system stays the system of record; the local
/// rows only exist to refuse duplicates and to find ambiguous writes again.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    /// <summary>
    /// A pending claim older than this cannot belong to a live request (every request's billing work is bounded far
    /// below it), so it is reconciled against the billing system instead of being honoured.
    /// </summary>
    public static readonly TimeSpan ClaimTimeToLive = TimeSpan.FromSeconds(60);

    private const int MaxClaimAttempts = 3;

    private readonly ISubscriptionBillingGateway _gateway;
    private readonly ISubscriptionClaimStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly IAppLogger<SubscriptionService> _logger;

    public SubscriptionService(ISubscriptionBillingGateway gateway,
        ISubscriptionClaimStore store,
        TimeProvider timeProvider,
        IAppLogger<SubscriptionService> logger)
    {
        _gateway = gateway;
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTimeOffset Now => _timeProvider.GetUtcNow();

    /// <summary>
    /// The deterministic customer reference for a buyer. Opaque (no e-mail address in it) and stable across restarts,
    /// so the billing customer can always be found again even if the local store is lost.
    /// </summary>
    public static string CustomerReferenceFor(string buyerId)
    {
        Guard.Against.NullOrWhiteSpace(buyerId, nameof(buyerId));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(buyerId.Trim().ToUpperInvariant()));
        return "eshop-" + Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
    }

    private static string NewSubscriptionReference() => "eshop-sub-" + Guid.NewGuid().ToString("N");

    public Task<PlanCatalog> GetPlansAsync(CancellationToken cancellationToken = default) =>
        _gateway.ListPlansAsync(cancellationToken);

    public async Task<SubscribeResult> SubscribeAsync(Subscriber subscriber, string planHandle,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(subscriber, nameof(subscriber));
        Guard.Against.NullOrWhiteSpace(planHandle, nameof(planHandle));

        // Only a plan the configured family actually offers may be subscribed to.
        var catalog = await _gateway.ListPlansAsync(cancellationToken);
        var plan = catalog.Find(planHandle.Trim());
        if (plan is null)
        {
            return SubscribeResult.UnknownPlan();
        }

        SubscriptionEnrollment? claim = null;
        for (var attempt = 0; attempt < MaxClaimAttempts && claim is null; attempt++)
        {
            var candidate = new SubscriptionEnrollment(subscriber.BuyerId, plan.Handle, NewSubscriptionReference(), Now);
            if (await _store.TryClaimEnrollmentAsync(candidate, cancellationToken))
            {
                claim = candidate;
                break;
            }

            var existing = await _store.GetEnrollmentAsync(subscriber.BuyerId, plan.Handle, cancellationToken);
            if (existing is null)
            {
                continue; // released by another request between our insert and our read — try again
            }

            if (existing.Status == EnrollmentStatus.Pending && !existing.IsStale(Now, ClaimTimeToLive))
            {
                return SubscribeResult.InProgress(plan);
            }

            var held = await FindHeldSubscriptionAsync(subscriber.BuyerId, existing, cancellationToken);
            if (held is not null && !held.IsTerminal)
            {
                return SubscribeResult.Existing(held, plan);
            }

            // Ended at the billing system (canceled/expired), or a stale claim the billing system never acted on:
            // drop it and let the primary key arbitrate a fresh claim.
            _logger.LogInformation("Releasing enrollment claim for plan {PlanHandle} (status {Status}, provider state {State}).",
                plan.Handle, existing.Status, held?.State ?? "none");
            await _store.ReleaseEnrollmentAsync(existing, cancellationToken);
        }

        if (claim is null)
        {
            return SubscribeResult.InProgress(plan);
        }

        BillingSubscription subscription;
        var subscriptionWriteSent = false;
        try
        {
            var customerId = await EnsureCustomerAsync(subscriber, cancellationToken);

            // The billing system is the system of record: if it already holds a live subscription to this plan
            // (e.g. the local store was reset), adopt it instead of creating a second one.
            var held = (await _gateway.ListCustomerSubscriptionsAsync(customerId, cancellationToken))
                .FirstOrDefault(s => !s.IsTerminal && string.Equals(s.PlanHandle, plan.Handle, StringComparison.OrdinalIgnoreCase));
            if (held is not null)
            {
                claim.MarkActive(held.Id, Now);
                await _store.SaveEnrollmentAsync(claim, CancellationToken.None);
                return SubscribeResult.Existing(held, plan);
            }

            subscriptionWriteSent = true;
            subscription = await _gateway.CreateSubscriptionAsync(customerId, plan.Handle, claim.SubscriptionReference, cancellationToken);
        }
        catch (BillingOutcomeUnknownException)
        {
            // The subscription may exist. Keep the claim pending: the next POST or GET settles it by its reference.
            _logger.LogWarning("Subscription outcome for plan {PlanHandle} is unknown; claim {Reference} left pending.",
                plan.Handle, claim.SubscriptionReference);
            throw;
        }
        catch (OperationCanceledException) when (subscriptionWriteSent)
        {
            // The caller went away mid-write; same as an unknown outcome.
            throw;
        }
        catch
        {
            // Nothing reached the subscription endpoint, or the billing system definitively refused it.
            await _store.ReleaseEnrollmentAsync(claim, CancellationToken.None);
            throw;
        }

        claim.MarkActive(subscription.Id, Now);
        await _store.SaveEnrollmentAsync(claim, CancellationToken.None);
        _logger.LogInformation("Created subscription {SubscriptionId} on plan {PlanHandle}.", subscription.Id, plan.Handle);
        return SubscribeResult.Created(subscription, plan);
    }

    public async Task<IReadOnlyList<BillingSubscription>> GetSubscriptionsAsync(string buyerId,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(buyerId, nameof(buyerId));

        var customerId = await FindCustomerIdAsync(buyerId, cancellationToken);
        if (customerId is null)
        {
            return Array.Empty<BillingSubscription>();
        }

        var subscriptions = await _gateway.ListCustomerSubscriptionsAsync(customerId.Value, cancellationToken);
        await SettlePendingEnrollmentsAsync(buyerId, subscriptions, cancellationToken);
        return subscriptions.OrderByDescending(s => s.CreatedAt).ToList();
    }

    /// <summary>
    /// Settles enrollments whose create call ended without a confirmed outcome, by matching the reference they were
    /// sent with against what the billing system holds.
    /// </summary>
    private async Task SettlePendingEnrollmentsAsync(string buyerId, IReadOnlyList<BillingSubscription> subscriptions,
        CancellationToken cancellationToken)
    {
        var enrollments = await _store.ListEnrollmentsAsync(buyerId, cancellationToken);
        foreach (var enrollment in enrollments.Where(e => e.Status == EnrollmentStatus.Pending))
        {
            var match = subscriptions.FirstOrDefault(s => s.Reference == enrollment.SubscriptionReference);
            if (match is null)
            {
                continue;
            }

            enrollment.MarkActive(match.Id, Now);
            await _store.SaveEnrollmentAsync(enrollment, CancellationToken.None);
            _logger.LogInformation("Settled pending enrollment for plan {PlanHandle} as subscription {SubscriptionId}.",
                enrollment.PlanHandle, match.Id);
        }
    }

    private async Task<BillingSubscription?> FindHeldSubscriptionAsync(string buyerId, SubscriptionEnrollment enrollment,
        CancellationToken cancellationToken)
    {
        var customerId = await FindCustomerIdAsync(buyerId, cancellationToken);
        if (customerId is null)
        {
            return null;
        }

        var subscriptions = await _gateway.ListCustomerSubscriptionsAsync(customerId.Value, cancellationToken);
        var match = (enrollment.ProviderSubscriptionId is int id ? subscriptions.FirstOrDefault(s => s.Id == id) : null)
            ?? subscriptions.FirstOrDefault(s => s.Reference == enrollment.SubscriptionReference);

        if (match is not null && enrollment.Status == EnrollmentStatus.Pending)
        {
            enrollment.MarkActive(match.Id, Now);
            await _store.SaveEnrollmentAsync(enrollment, CancellationToken.None);
        }

        return match;
    }

    /// <summary>The buyer's billing customer id from the local link, else from the billing system by reference.</summary>
    private async Task<int?> FindCustomerIdAsync(string buyerId, CancellationToken cancellationToken)
    {
        var row = await _store.GetCustomerAsync(buyerId, cancellationToken);
        if (row?.ProviderCustomerId is int knownId)
        {
            return knownId;
        }

        var reference = row?.CustomerReference ?? CustomerReferenceFor(buyerId);
        var customerId = await _gateway.FindCustomerIdByReferenceAsync(reference, cancellationToken);
        if (customerId is null)
        {
            return null;
        }

        // Remember the link (settles a pending customer claim, or re-links after the local store was reset).
        if (row is null)
        {
            row = new BillingCustomer(buyerId, reference, Now);
            if (!await _store.TryClaimCustomerAsync(row, cancellationToken))
            {
                return customerId;
            }
        }

        row.MarkProvisioned(customerId.Value, Now);
        await _store.SaveCustomerAsync(row, CancellationToken.None);
        return customerId;
    }

    private async Task<int> EnsureCustomerAsync(Subscriber subscriber, CancellationToken cancellationToken)
    {
        var row = await _store.GetCustomerAsync(subscriber.BuyerId, cancellationToken);
        if (row?.ProviderCustomerId is int knownId)
        {
            return knownId;
        }

        var claimedByUs = false;
        if (row is null)
        {
            var candidate = new BillingCustomer(subscriber.BuyerId, CustomerReferenceFor(subscriber.BuyerId), Now);
            if (await _store.TryClaimCustomerAsync(candidate, cancellationToken))
            {
                row = candidate;
                claimedByUs = true;
            }
            else
            {
                row = await _store.GetCustomerAsync(subscriber.BuyerId, cancellationToken)
                    ?? throw new BillingOperationInProgressException("Your billing account is being set up by another request. Try again in a moment.");
                if (row.ProviderCustomerId is int raceWinnerId)
                {
                    return raceWinnerId;
                }
            }
        }

        // The billing system may already hold this customer: created by a request whose outcome was unknown, by a
        // concurrent request, or before the local store was reset.
        var existingId = await _gateway.FindCustomerIdByReferenceAsync(row.CustomerReference, cancellationToken);
        if (existingId is int foundId)
        {
            return await RecordCustomerAsync(row, foundId);
        }

        if (!claimedByUs && !row.IsStale(Now, ClaimTimeToLive))
        {
            throw new BillingOperationInProgressException("Your billing account is being set up by another request. Try again in a moment.");
        }

        int customerId;
        try
        {
            customerId = await _gateway.CreateCustomerAsync(
                new NewBillingCustomer(row.CustomerReference, subscriber.Email, subscriber.FirstName, subscriber.LastName),
                cancellationToken);
        }
        catch (BillingOutcomeUnknownException)
        {
            throw; // claim stays pending; the reference lookup above settles it next time
        }
        catch (BillingProviderException) when (claimedByUs)
        {
            await _store.ReleaseCustomerAsync(row, CancellationToken.None);
            throw;
        }

        return await RecordCustomerAsync(row, customerId);
    }

    private async Task<int> RecordCustomerAsync(BillingCustomer row, int customerId)
    {
        row.MarkProvisioned(customerId, Now);
        await _store.SaveCustomerAsync(row, CancellationToken.None);
        return customerId;
    }
}
