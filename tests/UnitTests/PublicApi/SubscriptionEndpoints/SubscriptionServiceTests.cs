using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.PublicApi.Maxio;
using Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.PublicApi.SubscriptionEndpoints;

public class SubscriptionServiceTests
{
    private const string FamilyHandle = "eshop-subscribe";
    private static readonly ApplicationUser s_user = new()
    {
        Id = "user-1",
        UserName = "jane.doe@example.com",
        Email = "jane.doe@example.com"
    };

    private readonly IMaxioClient _maxio = Substitute.For<IMaxioClient>();
    private readonly SubscriptionService _sut;

    public SubscriptionServiceTests()
    {
        _sut = new SubscriptionService(
            _maxio,
            new SubscriptionGate(),
            Options.Create(new MaxioOptions
            {
                ApiKey = "test-key",
                Subdomain = "test-site",
                ProductFamilyHandle = FamilyHandle
            }));
    }

    private static MaxioProduct FamilyProduct(string handle) => new()
    {
        Handle = handle,
        Name = $"{handle} plan",
        ProductFamily = new MaxioProductFamily { Handle = FamilyHandle }
    };

    [Fact]
    public async Task Subscribe_creates_customer_and_subscription_on_first_use()
    {
        var customer = new MaxioCustomer { Id = 123, Reference = "eshop-user-user-1" };
        _maxio.LookupCustomerByReferenceAsync("eshop-user-user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<MaxioCustomer?>(null));
        _maxio.CreateCustomerAsync(Arg.Any<CreateMaxioCustomerRequest>(), Arg.Any<CancellationToken>())
            .Returns(customer);
        _maxio.ReadProductByHandleAsync("eshop-pro", Arg.Any<CancellationToken>())
            .Returns(FamilyProduct("eshop-pro"));
        _maxio.ListCustomerSubscriptionsAsync(123, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MaxioSubscription>());
        _maxio.CreateSubscriptionAsync(Arg.Any<CreateMaxioSubscriptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MaxioSubscription { Id = 456, State = "active", Product = FamilyProduct("eshop-pro") });

        var result = await _sut.SubscribeAsync(s_user, "eshop-pro");

        Assert.Equal(456, result.Id);
        await _maxio.Received(1).CreateCustomerAsync(
            Arg.Is<CreateMaxioCustomerRequest>(r =>
                r.Customer.Reference == "eshop-user-user-1" &&
                r.Customer.Email == "jane.doe@example.com" &&
                !string.IsNullOrWhiteSpace(r.Customer.FirstName) &&
                !string.IsNullOrWhiteSpace(r.Customer.LastName)),
            Arg.Any<CancellationToken>());
        await _maxio.Received(1).CreateSubscriptionAsync(
            Arg.Is<CreateMaxioSubscriptionRequest>(r =>
                r.Subscription.ProductHandle == "eshop-pro" &&
                r.Subscription.CustomerId == 123 &&
                r.Subscription.PaymentCollectionMethod == "remittance" &&
                r.Subscription.Reference == "eshop-user-user-1:eshop-pro"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Subscribe_is_idempotent_when_a_live_subscription_already_exists()
    {
        _maxio.LookupCustomerByReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 123, Reference = "eshop-user-user-1" });
        _maxio.ReadProductByHandleAsync("eshop-pro", Arg.Any<CancellationToken>())
            .Returns(FamilyProduct("eshop-pro"));
        _maxio.ListCustomerSubscriptionsAsync(123, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new MaxioSubscription { Id = 456, State = "active", Product = FamilyProduct("eshop-pro") }
            });

        var result = await _sut.SubscribeAsync(s_user, "eshop-pro");

        Assert.Equal(456, result.Id);
        await _maxio.DidNotReceive().CreateCustomerAsync(Arg.Any<CreateMaxioCustomerRequest>(), Arg.Any<CancellationToken>());
        await _maxio.DidNotReceive().CreateSubscriptionAsync(Arg.Any<CreateMaxioSubscriptionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Subscribe_resubscribes_after_the_previous_subscription_ended()
    {
        _maxio.LookupCustomerByReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 123 });
        _maxio.ReadProductByHandleAsync("eshop-pro", Arg.Any<CancellationToken>())
            .Returns(FamilyProduct("eshop-pro"));
        _maxio.ListCustomerSubscriptionsAsync(123, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new MaxioSubscription { Id = 111, State = "canceled", Product = FamilyProduct("eshop-pro") }
            });
        _maxio.CreateSubscriptionAsync(Arg.Any<CreateMaxioSubscriptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MaxioSubscription { Id = 222, State = "active", Product = FamilyProduct("eshop-pro") });

        var result = await _sut.SubscribeAsync(s_user, "eshop-pro");

        Assert.Equal(222, result.Id);
        await _maxio.Received(1).CreateSubscriptionAsync(Arg.Any<CreateMaxioSubscriptionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Subscribe_recovers_when_customer_creation_hits_a_reference_race()
    {
        var customer = new MaxioCustomer { Id = 123, Reference = "eshop-user-user-1" };
        _maxio.LookupCustomerByReferenceAsync("eshop-user-user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<MaxioCustomer?>(null), Task.FromResult<MaxioCustomer?>(customer));
        _maxio.CreateCustomerAsync(Arg.Any<CreateMaxioCustomerRequest>(), Arg.Any<CancellationToken>())
            .Returns<MaxioCustomer>(_ => throw new MaxioApiException(422, new[] { "Reference: has already been taken" }));
        _maxio.ReadProductByHandleAsync("eshop-pro", Arg.Any<CancellationToken>())
            .Returns(FamilyProduct("eshop-pro"));
        _maxio.ListCustomerSubscriptionsAsync(123, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MaxioSubscription>());
        _maxio.CreateSubscriptionAsync(Arg.Any<CreateMaxioSubscriptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MaxioSubscription { Id = 456, State = "active", Product = FamilyProduct("eshop-pro") });

        var result = await _sut.SubscribeAsync(s_user, "eshop-pro");

        Assert.Equal(456, result.Id);
        await _maxio.Received(1).CreateSubscriptionAsync(
            Arg.Is<CreateMaxioSubscriptionRequest>(r => r.Subscription.CustomerId == 123),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Subscribe_rejects_a_plan_outside_the_configured_family()
    {
        _maxio.LookupCustomerByReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 123 });
        _maxio.ReadProductByHandleAsync("other-plan", Arg.Any<CancellationToken>())
            .Returns(new MaxioProduct
            {
                Handle = "other-plan",
                ProductFamily = new MaxioProductFamily { Handle = "some-other-family" }
            });

        await Assert.ThrowsAsync<SubscriptionPlanNotFoundException>(() => _sut.SubscribeAsync(s_user, "other-plan"));
    }

    [Fact]
    public async Task Subscribe_rejects_an_unknown_plan()
    {
        _maxio.LookupCustomerByReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 123 });
        _maxio.ReadProductByHandleAsync("missing", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<MaxioProduct?>(null));

        await Assert.ThrowsAsync<SubscriptionPlanNotFoundException>(() => _sut.SubscribeAsync(s_user, "missing"));
    }

    [Fact]
    public async Task GetSubscriptions_returns_empty_list_when_user_has_no_maxio_customer()
    {
        _maxio.LookupCustomerByReferenceAsync("eshop-user-user-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<MaxioCustomer?>(null));

        var result = await _sut.GetSubscriptionsAsync(s_user);

        Assert.Empty(result);
        await _maxio.DidNotReceive().ListCustomerSubscriptionsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSubscriptions_lists_the_customers_subscriptions()
    {
        _maxio.LookupCustomerByReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 123 });
        _maxio.ListCustomerSubscriptionsAsync(123, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new MaxioSubscription { Id = 1, State = "active", Product = FamilyProduct("eshop-pro") },
                new MaxioSubscription { Id = 2, State = "active", Product = FamilyProduct("basic-plan") }
            });

        var result = await _sut.GetSubscriptionsAsync(s_user);

        Assert.Equal(new[] { 1, 2 }, result.Select(s => s.Id).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task Subscribe_gate_serializes_concurrent_calls_for_the_same_user()
    {
        _maxio.LookupCustomerByReferenceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 123 });
        _maxio.ReadProductByHandleAsync("eshop-pro", Arg.Any<CancellationToken>())
            .Returns(FamilyProduct("eshop-pro"));

        var sync = new object();
        var subscriptions = new List<MaxioSubscription>();
        var created = 0;
        _maxio.ListCustomerSubscriptionsAsync(123, Arg.Any<CancellationToken>())
            .Returns(_ => { lock (sync) { return subscriptions.ToArray(); } });
        _maxio.CreateSubscriptionAsync(Arg.Any<CreateMaxioSubscriptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                lock (sync)
                {
                    created++;
                    var subscription = new MaxioSubscription { Id = created, State = "active", Product = FamilyProduct("eshop-pro") };
                    subscriptions.Add(subscription);
                    return subscription;
                }
            });

        var results = await Task.WhenAll(
            _sut.SubscribeAsync(s_user, "eshop-pro"),
            _sut.SubscribeAsync(s_user, "eshop-pro"),
            _sut.SubscribeAsync(s_user, "eshop-pro"));

        // All calls resolve to the first-created subscription - no duplicates.
        Assert.Single(results.Select(r => r.Id).Distinct());
    }
}