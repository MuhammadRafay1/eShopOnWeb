using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.PublicApi.Maxio;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Thrown when a shopper requests a plan that does not exist (or is outside the
/// configured Maxio product family).
/// </summary>
public class SubscriptionPlanNotFoundException : Exception
{
    public SubscriptionPlanNotFoundException(string planHandle)
        : base($"No subscription plan with handle '{planHandle}' was found.")
    {
    }
}

/// <summary>
/// Serializes subscription operations per user (single instance per app) so that a
/// double-click (or concurrent retries) can never create duplicate customers or
/// subscriptions, regardless of request count.
/// </summary>
public interface ISubscriptionGate
{
    Task<IDisposable> AcquireAsync(string userId, CancellationToken cancellationToken);
}

public sealed class SubscriptionGate : ISubscriptionGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _perUserLocks = new();

    public async Task<IDisposable> AcquireAsync(string userId, CancellationToken cancellationToken)
    {
        var semaphore = _perUserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Orchestrates the subscription capability against Maxio Advanced Billing (the billing
/// system of record). eShopOnWeb users are mapped to Maxio customers via a deterministic,
/// unique customer reference, so no local userId-to-subscription mapping is required and
/// the flow is idempotent across restarts.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Subscribes the given user to the plan identified by <paramref name="planHandle"/>.
    /// Idempotent: a Maxio customer is created on first use (never twice, thanks to the
    /// unique customer reference), and an existing, still-live subscription to the same
    /// plan is returned instead of creating a duplicate.
    /// </summary>
    Task<MaxioSubscription> SubscribeAsync(ApplicationUser user, string planHandle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the Maxio subscriptions that belong to the given user. Returns an empty
    /// list when the user has no Maxio customer record yet.
    /// </summary>
    Task<IReadOnlyList<MaxioSubscription>> GetSubscriptionsAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}

public class SubscriptionService : ISubscriptionService
{
    private const string CustomerReferencePrefix = "eshop-user-";
    private const string PaymentCollectionMethodRemittance = "remittance";

    /// <summary>
    /// Subscription states in which a subscription is considered "live" and must not be duplicated.
    /// </summary>
    private static readonly HashSet<string> s_liveStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "active", "trialing", "awaiting_signup"
    };

    private readonly IMaxioClient _maxio;
    private readonly ISubscriptionGate _gate;
    private readonly MaxioOptions _options;

    public SubscriptionService(IMaxioClient maxio, ISubscriptionGate gate, IOptions<MaxioOptions> options)
    {
        _maxio = maxio;
        _gate = gate;
        _options = options.Value;
    }

    public async Task<MaxioSubscription> SubscribeAsync(ApplicationUser user, string planHandle, CancellationToken cancellationToken = default)
    {
        using (await _gate.AcquireAsync(user.Id, cancellationToken))
        {
            var customer = await GetOrCreateCustomerAsync(user, cancellationToken);

            var product = await _maxio.ReadProductByHandleAsync(planHandle, cancellationToken);
            if (product is null || !string.Equals(product.ProductFamily?.Handle, _options.ProductFamilyHandle, StringComparison.OrdinalIgnoreCase))
            {
                throw new SubscriptionPlanNotFoundException(planHandle);
            }

            // Idempotency: a double-click (or a retry) must never create a second subscription.
            var existing = await FindLiveSubscriptionAsync(customer.Id, planHandle, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            return await _maxio.CreateSubscriptionAsync(new CreateMaxioSubscriptionRequest
            {
                Subscription = new CreateMaxioSubscriptionRequest.SubscriptionAttributes
                {
                    ProductHandle = planHandle,
                    CustomerId = customer.Id,
                    // The seeded plans do not require a payment method; remittance (invoice)
                    // collection keeps signup card-free.
                    PaymentCollectionMethod = PaymentCollectionMethodRemittance,
                    Reference = GetSubscriptionReference(user.Id, planHandle)
                }
            }, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<MaxioSubscription>> GetSubscriptionsAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var customer = await _maxio.LookupCustomerByReferenceAsync(GetCustomerReference(user.Id), cancellationToken);
        if (customer is null)
        {
            return Array.Empty<MaxioSubscription>();
        }

        return await _maxio.ListCustomerSubscriptionsAsync(customer.Id, cancellationToken);
    }

    /// <summary>
    /// Gets the Maxio customer for the eShopOnWeb user, creating it on first use.
    /// Idempotent: the deterministic, unique customer reference is looked up first, and a
    /// create race is resolved by looking the customer back up after a 422.
    /// </summary>
    private async Task<MaxioCustomer> GetOrCreateCustomerAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var reference = GetCustomerReference(user.Id);

        var existing = await _maxio.LookupCustomerByReferenceAsync(reference, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        try
        {
            var (firstName, lastName) = SplitName(user.UserName ?? user.Email ?? "eShop User");
            return await _maxio.CreateCustomerAsync(new CreateMaxioCustomerRequest
            {
                Customer = new CreateMaxioCustomerRequest.CustomerAttributes
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = user.Email ?? user.UserName ?? $"{user.Id}@users.eshopweb.invalid",
                    Reference = reference
                }
            }, cancellationToken);
        }
        catch (MaxioApiException ex) when (ex.StatusCode == 422)
        {
            // A concurrent request (e.g. outside this process) may have created the
            // customer first - the unique reference makes this recoverable.
            existing = await _maxio.LookupCustomerByReferenceAsync(reference, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            throw;
        }
    }

    private async Task<MaxioSubscription?> FindLiveSubscriptionAsync(int customerId, string planHandle, CancellationToken cancellationToken)
    {
        var subscriptions = await _maxio.ListCustomerSubscriptionsAsync(customerId, cancellationToken);
        return subscriptions.FirstOrDefault(s =>
            string.Equals(s.Product?.Handle, planHandle, StringComparison.OrdinalIgnoreCase) &&
            s_liveStates.Contains(s.State));
    }

    private static string GetCustomerReference(string userId) => CustomerReferencePrefix + userId;

    private static string GetSubscriptionReference(string userId, string planHandle) =>
        $"{CustomerReferencePrefix}{userId}:{planHandle}";

    private static (string FirstName, string LastName) SplitName(string userName)
    {
        var name = userName.Contains('@') ? userName.Split('@')[0] : userName;
        var separators = new[] { '.', '_', ' ', '-' };
        var parts = name.Split(separators, 2);
        return parts.Length == 2
            ? (Capitalize(parts[0]), Capitalize(parts[1]))
            : (Capitalize(name), "eShop Customer");
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
}