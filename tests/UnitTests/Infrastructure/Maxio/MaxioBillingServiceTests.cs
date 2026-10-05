using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Maxio;
using Microsoft.eShopWeb.Infrastructure.Maxio.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Maxio;

public class MaxioBillingServiceTests
{
    private const string FamilyHandle = "eshop-subscribe";
    private const string ProPlanHandle = "eshop-pro";

    private static readonly MaxioOptions _options = new()
    {
        ApiKey = "test-api-key",
        Subdomain = "test-site",
        ProductFamilyHandle = FamilyHandle
    };

    private readonly IMaxioClient _client = Substitute.For<IMaxioClient>();

    private MaxioBillingService CreateService()
    {
        return new MaxioBillingService(
            _client,
            Options.Create(_options),
            new MemoryCache(new MemoryCacheOptions()),
            Substitute.For<IAppLogger<MaxioBillingService>>());
    }

    private static Subscriber Subscriber(string userId = "user-1") => new()
    {
        UserId = userId,
        Email = "user@example.com",
        FirstName = "User",
        LastName = "eShop Customer"
    };

    private void SetUpCatalog(params MaxioProduct[] products)
    {
        _client.GetProductFamilyByHandleAsync(FamilyHandle, Arg.Any<CancellationToken>())
            .Returns(new MaxioProductFamily { Id = 302, Handle = FamilyHandle });
        _client.ListProductsAsync(302, Arg.Any<CancellationToken>())
            .Returns(products.ToList());
    }

    private static MaxioProduct Plan(string handle = ProPlanHandle, int priceInCents = 29900, DateTimeOffset? archivedAt = null) => new()
    {
        Id = 71,
        Handle = handle,
        Name = "Pro Plan",
        PriceInCents = priceInCents,
        Interval = 1,
        IntervalUnit = "month",
        ArchivedAt = archivedAt
    };

    private static MaxioSubscription Subscription(int id = 946, string state = "active", string handle = ProPlanHandle) => new()
    {
        Id = id,
        State = state,
        ProductPriceInCents = 29900,
        NextAssessmentAt = DateTimeOffset.Parse("2026-11-05T17:24:00+05:00"),
        CurrentPeriodEndsAt = DateTimeOffset.Parse("2026-11-05T17:24:00+05:00"),
        ActivatedAt = DateTimeOffset.Parse("2026-10-05T17:24:00+05:00"),
        CreatedAt = DateTimeOffset.Parse("2026-10-05T17:24:00+05:00"),
        Product = new MaxioSubscriptionProduct { Id = 71, Handle = handle, Name = "Pro Plan", PriceInCents = 29900 },
        Customer = new MaxioSubscriptionCustomer { Id = 992, Reference = "eshop-user-user-1" }
    };

    [Fact]
    public async Task GetPlansAsync_ReturnsActivePlansOnly()
    {
        SetUpCatalog(Plan(), Plan("basic-plan", 2900), Plan("archived-plan", 100, DateTimeOffset.Now));
        var service = CreateService();

        var plans = await service.GetPlansAsync();

        Assert.Equal(2, plans.Count);
        Assert.All(plans, p => Assert.DoesNotContain("archived", p.Handle));
        var pro = Assert.Single(plans, p => p.Handle == ProPlanHandle);
        Assert.Equal(29900, pro.PriceInCents);
        Assert.Equal("$299.00", pro.FormattedPrice);
        Assert.Equal("month", pro.IntervalUnit);
    }

    [Fact]
    public async Task SubscribeAsync_CreatesCustomerAndSubscription()
    {
        SetUpCatalog(Plan());
        _client.GetCustomerByReferenceAsync("eshop-user-user-1", Arg.Any<CancellationToken>()).Returns((MaxioCustomer?)null);
        _client.CreateCustomerAsync("User", "eShop Customer", "user@example.com", "eshop-user-user-1", Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 992, Reference = "eshop-user-user-1" });
        _client.ListCustomerSubscriptionsAsync(992, Arg.Any<CancellationToken>()).Returns(new List<MaxioSubscription>());
        _client.CreateSubscriptionAsync(992, ProPlanHandle, Arg.Any<CancellationToken>()).Returns(Subscription());
        var service = CreateService();

        var result = await service.SubscribeAsync(Subscriber(), ProPlanHandle);

        Assert.False(result.AlreadySubscribed);
        Assert.Equal(946, result.Subscription.Id);
        Assert.Equal("active", result.Subscription.State);
        Assert.Equal(ProPlanHandle, result.Subscription.PlanHandle);
        Assert.Equal(992, result.Subscription.CustomerId);
        Assert.Equal("eshop-user-user-1", result.Subscription.CustomerReference);
        Assert.Equal(2026, result.Subscription.NextBillingAt!.Value.Year);
    }

    [Fact]
    public async Task SubscribeAsync_ReturnsExistingSubscription_WhenUserAlreadySubscribed()
    {
        SetUpCatalog(Plan());
        _client.GetCustomerByReferenceAsync("eshop-user-user-1", Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 992, Reference = "eshop-user-user-1" });
        _client.ListCustomerSubscriptionsAsync(992, Arg.Any<CancellationToken>())
            .Returns(new List<MaxioSubscription> { Subscription() });
        var service = CreateService();

        var result = await service.SubscribeAsync(Subscriber(), ProPlanHandle);

        Assert.True(result.AlreadySubscribed);
        Assert.Equal(946, result.Subscription.Id);
        await _client.DidNotReceive().CreateSubscriptionAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeAsync_IgnoresCanceledSubscriptions_AndCreatesNewOne()
    {
        SetUpCatalog(Plan());
        _client.GetCustomerByReferenceAsync("eshop-user-user-1", Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 992, Reference = "eshop-user-user-1" });
        _client.ListCustomerSubscriptionsAsync(992, Arg.Any<CancellationToken>())
            .Returns(new List<MaxioSubscription> { Subscription(id: 900, state: "canceled") });
        _client.CreateSubscriptionAsync(992, ProPlanHandle, Arg.Any<CancellationToken>()).Returns(Subscription(id: 901));
        var service = CreateService();

        var result = await service.SubscribeAsync(Subscriber(), ProPlanHandle);

        Assert.False(result.AlreadySubscribed);
        Assert.Equal(901, result.Subscription.Id);
    }

    [Fact]
    public async Task SubscribeAsync_Throws_WhenPlanHandleIsUnknown()
    {
        SetUpCatalog(Plan());
        var service = CreateService();

        await Assert.ThrowsAsync<SubscriptionPlanNotFoundException>(
            () => service.SubscribeAsync(Subscriber(), "no-such-plan"));
    }

    [Fact]
    public async Task SubscribeAsync_Recovers_WhenCustomerIsCreatedConcurrently()
    {
        SetUpCatalog(Plan());
        _client.GetCustomerByReferenceAsync("eshop-user-user-1", Arg.Any<CancellationToken>())
            .Returns((MaxioCustomer?)null, new MaxioCustomer { Id = 992, Reference = "eshop-user-user-1" });
        _client.CreateCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new MaxioApiException((HttpStatusCode)422, "Reference: must be unique"));
        _client.ListCustomerSubscriptionsAsync(992, Arg.Any<CancellationToken>()).Returns(new List<MaxioSubscription>());
        _client.CreateSubscriptionAsync(992, ProPlanHandle, Arg.Any<CancellationToken>()).Returns(Subscription());
        var service = CreateService();

        var result = await service.SubscribeAsync(Subscriber(), ProPlanHandle);

        Assert.False(result.AlreadySubscribed);
        await _client.Received(1).CreateCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSubscriptionsAsync_ReturnsEmpty_WhenCustomerDoesNotExist()
    {
        _client.GetCustomerByReferenceAsync("eshop-user-user-1", Arg.Any<CancellationToken>()).Returns((MaxioCustomer?)null);
        var service = CreateService();

        var subscriptions = await service.GetSubscriptionsAsync("user-1");

        Assert.Empty(subscriptions);
    }

    [Fact]
    public async Task GetSubscriptionsAsync_ReturnsSubscriptions_NewestFirst()
    {
        _client.GetCustomerByReferenceAsync("eshop-user-user-1", Arg.Any<CancellationToken>())
            .Returns(new MaxioCustomer { Id = 992, Reference = "eshop-user-user-1" });
        _client.ListCustomerSubscriptionsAsync(992, Arg.Any<CancellationToken>())
            .Returns(new List<MaxioSubscription>
            {
                Subscription(id: 1),
                Subscription(id: 2, handle: "basic-plan")
            });
        var service = CreateService();

        var subscriptions = await service.GetSubscriptionsAsync("user-1");

        Assert.Equal(2, subscriptions.Count);
    }

    [Fact]
    public void Constructor_Throws_WhenConfigurationIsIncomplete()
    {
        var incomplete = new MaxioOptions { Subdomain = "test-site", ProductFamilyHandle = FamilyHandle };

        var ex = Assert.Throws<InvalidOperationException>(() => new MaxioBillingService(
            _client,
            Options.Create(incomplete),
            new MemoryCache(new MemoryCacheOptions()),
            Substitute.For<IAppLogger<MaxioBillingService>>()));

        Assert.Contains("Maxio:ApiKey", ex.Message);
    }

    [Fact]
    public void BuildBaseUrl_UsesBaseUrlOverrideVerbatim()
    {
        var options = new MaxioOptions { ApiKey = "k", Subdomain = "ignored", BaseUrl = "https://custom.example.com/api/" };
        Assert.Equal("https://custom.example.com/api", MaxioClient.BuildBaseUrl(options));
    }

    [Fact]
    public void BuildBaseUrl_DerivesChargifyUrlFromSubdomain()
    {
        var options = new MaxioOptions { ApiKey = "k", Subdomain = "acme" };
        Assert.Equal("https://acme.chargify.com", MaxioClient.BuildBaseUrl(options));
    }
}