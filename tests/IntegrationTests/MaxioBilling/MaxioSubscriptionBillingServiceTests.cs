using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MaxioAdvancedBilling;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Billing;
using Microsoft.eShopWeb.Infrastructure.MaxioBilling;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.MaxioBilling;

/// <summary>
/// Exercises <see cref="MaxioSubscriptionBillingService"/> against a stub HTTP handler behind a
/// real SDK client — the SDK's own serialization, error shaping and (no-)retry behaviour are in
/// play, so these tests cover the wire contract, the idempotency rules and the unknown-outcome
/// reconciliation without touching the live provider.
/// </summary>
public class MaxioSubscriptionBillingServiceTests
{
    private const string FamilyHandle = "eshop-subscribe";
    private const string PlanHandle = "eshop-pro";

    private static readonly Subscriber Shopper = new("user-123", "jane.doe", "jane.doe@example.com");

    private static readonly string CustomerJson =
        """{"customer":{"id":999,"first_name":"jane","last_name":"doe","email":"jane.doe@example.com","reference":"eshop-user-user-123"}}""";

    private static readonly string SubscriptionJson =
        """{"subscription":{"id":555,"state":"active","product_price_in_cents":29900,"current_period_ends_at":"2026-11-05T12:00:00Z","next_assessment_at":"2026-11-05T12:00:00Z","activated_at":"2026-10-05T12:00:00Z","customer":{"id":999,"reference":"eshop-user-user-123"},"product":{"id":7126957,"name":"Pro Plan","handle":"eshop-pro"}}}""";

    private static readonly string ProductsJson =
        """[{"product":{"id":7126957,"name":"Pro Plan","handle":"eshop-pro","description":"eShop Pro","price_in_cents":29900,"interval":1,"interval_unit":"month","require_credit_card":false,"archived_at":null}},{"product":{"id":7126958,"name":"Basic Plan","handle":"basic-plan","description":"eShop Basic","price_in_cents":2900,"interval":1,"interval_unit":"month","require_credit_card":false,"archived_at":"2026-01-01T00:00:00Z"}}]""";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string?> Bodies { get; } = new();
        public string? LastBody => Bodies.Count == 0 ? null : Bodies[^1];

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? null : request.Content.ReadAsStringAsync().Result);
            var response = _responder(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private static MaxioSubscriptionBillingService NewService(StubHandler handler) =>
        new(
            new MaxioAdvancedBillingClient(new HttpClient(handler), new MaxioAdvancedBillingClientOptions()),
            Options.Create(new MaxioBillingOptions { ProductFamilyHandle = FamilyHandle }),
            NullLogger<MaxioSubscriptionBillingService>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);

    private static bool Is(HttpRequestMessage r, string method, string pathPart) =>
        r.Method.Method == method && r.RequestUri!.PathAndQuery.Contains(pathPart, StringComparison.Ordinal);

    private static bool SubscriptionCreated(HttpRequestMessage r) =>
        Is(r, "POST", "/subscriptions.json");

    [Fact]
    public async Task Subscribe_CreatesCustomerAndSubscription_AndSendsExpectedWirePayload()
    {
        var handler = new StubHandler(r =>
        {
            if (Is(r, "GET", "/subscriptions/lookup.json")) return NotFound();
            if (Is(r, "GET", "/product_families/")) return Json(HttpStatusCode.OK, ProductsJson);
            if (Is(r, "GET", "/customers/lookup.json")) return NotFound();
            if (Is(r, "POST", "/customers.json")) return Json(HttpStatusCode.OK, CustomerJson);
            if (SubscriptionCreated(r)) return Json(HttpStatusCode.OK, SubscriptionJson);
            throw new InvalidOperationException($"Unexpected request {r.Method} {r.RequestUri}");
        });

        var result = await NewService(handler).SubscribeAsync(Shopper, PlanHandle);

        Assert.Equal(555, result.Id);
        Assert.Equal("active", result.State);
        Assert.Equal(PlanHandle, result.PlanHandle);
        Assert.Equal("Pro Plan", result.PlanName);
        Assert.Equal(29900, result.PriceInCents);
        Assert.NotNull(result.NextBillingAt);

        var createBody = Assert.Single(handler.Bodies.Where(b => b is not null && b.Contains("\"subscription\"")));
        Assert.Contains("\"product_handle\":\"eshop-pro\"", createBody);
        Assert.Contains("\"customer_id\":999", createBody);
        Assert.Contains("\"reference\":\"eshop-sub-user-123-eshop-pro\"", createBody);
        Assert.Single(handler.Requests, r => SubscriptionCreated(r));
    }

    [Fact]
    public async Task Subscribe_Twice_ReturnsExistingSubscription_WithoutASecondCreate()
    {
        var subscriptionCreated = false;
        var handler = new StubHandler(r =>
        {
            if (Is(r, "GET", "/subscriptions/lookup.json"))
            {
                return subscriptionCreated ? Json(HttpStatusCode.OK, SubscriptionJson) : NotFound();
            }
            if (Is(r, "GET", "/product_families/")) return Json(HttpStatusCode.OK, ProductsJson);
            if (Is(r, "GET", "/customers/lookup.json"))
            {
                return subscriptionCreated ? Json(HttpStatusCode.OK, CustomerJson) : NotFound();
            }
            if (Is(r, "POST", "/customers.json")) return Json(HttpStatusCode.OK, CustomerJson);
            if (SubscriptionCreated(r))
            {
                subscriptionCreated = true;
                return Json(HttpStatusCode.OK, SubscriptionJson);
            }
            throw new InvalidOperationException($"Unexpected request {r.Method} {r.RequestUri}");
        });

        var service = NewService(handler);
        var first = await service.SubscribeAsync(Shopper, PlanHandle);
        var second = await service.SubscribeAsync(Shopper, PlanHandle);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(handler.Requests, r => SubscriptionCreated(r));
        // No second customer create either — idempotency covers the customer too.
        Assert.Single(handler.Requests, r => Is(r, "POST", "/customers.json"));
    }

    [Fact]
    public async Task Subscribe_ConcurrentDoubleClick_CreatesOnlyOneSubscription()
    {
        var subscriptionCreated = false;
        var createGate = new object();
        var handler = new StubHandler(r =>
        {
            if (Is(r, "GET", "/subscriptions/lookup.json"))
            {
                return subscriptionCreated ? Json(HttpStatusCode.OK, SubscriptionJson) : NotFound();
            }
            if (Is(r, "GET", "/product_families/")) return Json(HttpStatusCode.OK, ProductsJson);
            if (Is(r, "GET", "/customers/lookup.json"))
            {
                return subscriptionCreated ? Json(HttpStatusCode.OK, CustomerJson) : NotFound();
            }
            if (Is(r, "POST", "/customers.json")) return Json(HttpStatusCode.OK, CustomerJson);
            if (SubscriptionCreated(r))
            {
                lock (createGate)
                {
                    subscriptionCreated = true;
                }
                return Json(HttpStatusCode.OK, SubscriptionJson);
            }
            throw new InvalidOperationException($"Unexpected request {r.Method} {r.RequestUri}");
        });

        var service = NewService(handler);
        var results = await Task.WhenAll(
            service.SubscribeAsync(Shopper, PlanHandle),
            service.SubscribeAsync(Shopper, PlanHandle));

        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Single(handler.Requests, r => SubscriptionCreated(r));
    }

    [Fact]
    public async Task Subscribe_TransportFailureDuringCreate_SettlesOutcomeByReReading()
    {
        var subscriptionCreated = false;
        var handler = new StubHandler(r =>
        {
            if (Is(r, "GET", "/subscriptions/lookup.json"))
            {
                return subscriptionCreated ? Json(HttpStatusCode.OK, SubscriptionJson) : NotFound();
            }
            if (Is(r, "GET", "/product_families/")) return Json(HttpStatusCode.OK, ProductsJson);
            if (Is(r, "GET", "/customers/lookup.json")) return Json(HttpStatusCode.OK, CustomerJson);
            if (SubscriptionCreated(r))
            {
                // The write may have landed before the connection died — simulate exactly that.
                subscriptionCreated = true;
                throw new HttpRequestException("connection reset");
            }
            throw new InvalidOperationException($"Unexpected request {r.Method} {r.RequestUri}");
        });

        var result = await NewService(handler).SubscribeAsync(Shopper, PlanHandle);

        // The outcome was settled from the provider's state, not reported as a failure.
        Assert.Equal(555, result.Id);
        // The SDK never resends a POST on a transport failure — one create attempt happened.
        Assert.Single(handler.Requests, r => SubscriptionCreated(r));
    }

    [Fact]
    public async Task Subscribe_UnknownPlanHandle_IsRejectedBeforeAnyWrite()
    {
        var handler = new StubHandler(r =>
        {
            if (Is(r, "GET", "/subscriptions/lookup.json")) return NotFound();
            if (Is(r, "GET", "/product_families/")) return Json(HttpStatusCode.OK, ProductsJson);
            throw new InvalidOperationException($"Unexpected request {r.Method} {r.RequestUri}");
        });

        var ex = await Assert.ThrowsAsync<SubscriptionBillingException>(
            () => NewService(handler).SubscribeAsync(Shopper, "no-such-plan"));

        Assert.True(ex.CallerFault);
        Assert.Equal(404, ex.HttpStatusCode);
        Assert.Empty(handler.Requests.Where(r => r.Method.Method == "POST"));
    }

    [Fact]
    public async Task ListSubscriptions_NoCustomerYet_ReturnsEmpty()
    {
        var handler = new StubHandler(r =>
        {
            if (Is(r, "GET", "/customers/lookup.json")) return NotFound();
            throw new InvalidOperationException($"Unexpected request {r.Method} {r.RequestUri}");
        });

        var result = await NewService(handler).ListSubscriptionsAsync(Shopper);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListSubscriptions_ReturnsCustomersSubscriptions()
    {
        var handler = new StubHandler(r =>
        {
            if (Is(r, "GET", "/customers/lookup.json")) return Json(HttpStatusCode.OK, CustomerJson);
            if (Is(r, "GET", "/customers/999/subscriptions.json"))
            {
                return Json(HttpStatusCode.OK, $"[{SubscriptionJson}]");
            }
            throw new InvalidOperationException($"Unexpected request {r.Method} {r.RequestUri}");
        });

        var result = await NewService(handler).ListSubscriptionsAsync(Shopper);

        var summary = Assert.Single(result);
        Assert.Equal(555, summary.Id);
        Assert.Equal(PlanHandle, summary.PlanHandle);
        Assert.Equal("active", summary.State);
    }

    [Fact]
    public async Task ListPlans_MapsPlansAndSkipsArchivedProducts()
    {
        var handler = new StubHandler(r =>
        {
            if (Is(r, "GET", "/product_families/")) return Json(HttpStatusCode.OK, ProductsJson);
            throw new InvalidOperationException($"Unexpected request {r.Method} {r.RequestUri}");
        });

        var result = await NewService(handler).ListPlansAsync();

        var plan = Assert.Single(result);
        Assert.Equal(PlanHandle, plan.Handle);
        Assert.Equal("Pro Plan", plan.Name);
        Assert.Equal(29900, plan.PriceInCents);
        Assert.Equal("month", plan.IntervalUnit);
        Assert.False(plan.RequiresPaymentMethod);
        // A short page ends the walk — exactly one catalog request.
        Assert.Single(handler.Requests);
        Assert.Contains("per_page=200", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task ListPlans_FullPageWalksToNextPage()
    {
        var page1 = new StringBuilder("[");
        page1.Append(ProductsJson.Trim('[', ']'));
        for (var i = 0; i < 198; i++)
        {
            page1.Append(
                ",{\"product\":{\"id\":" + (900000 + i) +
                ",\"name\":\"Filler " + i +
                "\",\"handle\":\"filler-" + i +
                "\",\"price_in_cents\":100,\"interval\":1,\"interval_unit\":\"month\"}}");
        }
        page1.Append(']');

        var handler = new StubHandler(r =>
        {
            if (Is(r, "GET", "/product_families/"))
            {
                var isPage2 = r.RequestUri!.Query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Any(p => p.TrimStart('?') == "page=2");
                return isPage2
                    ? Json(HttpStatusCode.OK, """[{"product":{"id":8000001,"name":"Last","handle":"last","price_in_cents":100,"interval":1,"interval_unit":"month"}}]""")
                    : Json(HttpStatusCode.OK, page1.ToString());
            }
            throw new InvalidOperationException($"Unexpected request {r.Method} {r.RequestUri}");
        });

        var result = await NewService(handler).ListPlansAsync();

        Assert.Equal(200, result.Count);
        Assert.Equal(2, handler.Requests.Count);
    }
}