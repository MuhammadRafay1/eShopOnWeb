using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.SubscriptionEndpoints;

/// <summary>
/// An in-process stand-in for the Maxio API, plugged in as the SDK's HttpClient handler — no network.
/// It keeps customers and subscriptions in memory, records every request, and can be told to fail or hang.
/// </summary>
public sealed class FakeMaxio : HttpMessageHandler
{
    public const string BaseUrl = "https://maxio.offline.test";

    private readonly object _gate = new();
    private int _nextCustomerId = 1000;
    private int _nextSubscriptionId = 5000;

    public FakeMaxio(string productFamilyHandle = "test-family")
    {
        ProductFamilyHandle = productFamilyHandle;
    }

    public string ProductFamilyHandle { get; }

    public List<JsonObject> Products { get; } = new()
    {
        Product(7001, "eshop-pro", "Pro Plan", 29900),
        Product(7002, "basic-plan", "Basic Plan", 2900),
        Product(7003, "old-plan", "Retired Plan", 100, archived: true),
    };

    public ConcurrentDictionary<int, JsonObject> Customers { get; } = new();
    public ConcurrentDictionary<int, (int CustomerId, JsonObject Subscription)> Subscriptions { get; } = new();

    public ConcurrentQueue<(HttpMethod Method, string Path, string Query, string? Body)> Requests { get; } = new();

    /// <summary>Optional override: return a response (or throw) instead of the default behaviour.</summary>
    public Func<HttpRequestMessage, string?, HttpResponseMessage?>? Interceptor { get; set; }

    /// <summary>When true every request hangs until it is cancelled.</summary>
    public bool Hang { get; set; }

    /// <summary>Artificial latency for creates, so concurrent callers overlap.</summary>
    public TimeSpan CreateLatency { get; set; } = TimeSpan.Zero;

    public string? LastHost { get; private set; }

    public int Count(HttpMethod method, string path) => Requests.Count(r => r.Method == method && r.Path == path);

    public static JsonObject Product(int id, string handle, string name, long priceInCents, bool archived = false) => new()
    {
        ["id"] = id,
        ["handle"] = handle,
        ["name"] = name,
        ["price_in_cents"] = priceInCents,
        ["interval"] = 1,
        ["interval_unit"] = "month",
        ["archived_at"] = archived ? "2024-01-01T00:00:00Z" : null,
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer the body now: the SDK disposes request content once the attempt completes.
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        LastHost = request.RequestUri.Host;
        Requests.Enqueue((request.Method, path, request.RequestUri.Query, body));

        if (Hang)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        var intercepted = Interceptor?.Invoke(request, body);
        if (intercepted is not null)
        {
            return intercepted;
        }

        if (request.Method == HttpMethod.Post && CreateLatency > TimeSpan.Zero)
        {
            await Task.Delay(CreateLatency, cancellationToken);
        }

        return Route(request.Method, path, request.RequestUri.Query, body);
    }

    private HttpResponseMessage Route(HttpMethod method, string path, string query, string? body)
    {
        if (method == HttpMethod.Get && path == $"/product_families/handle:{ProductFamilyHandle}/products.json"
            || method == HttpMethod.Get && path == $"/product_families/handle%3A{ProductFamilyHandle}/products.json")
        {
            var page = int.Parse(QueryValue(query, "page") ?? "1");
            if (page > 1) return Json(HttpStatusCode.OK, new JsonArray());
            return Json(HttpStatusCode.OK, new JsonArray(Products.Select(p => (JsonNode)new JsonObject { ["product"] = p.DeepClone() }).ToArray()));
        }

        if (method == HttpMethod.Get && path.StartsWith("/product_families/"))
        {
            return Json(HttpStatusCode.NotFound, JsonValue.Create("Product family not found"));
        }

        if (method == HttpMethod.Get && path == "/customers/lookup.json")
        {
            var reference = Uri.UnescapeDataString(QueryValue(query, "reference") ?? "");
            var customer = Customers.Values.FirstOrDefault(c => (string?)c["reference"] == reference);
            return customer is null
                ? Json(HttpStatusCode.NotFound, new JsonObject { ["errors"] = new JsonArray("Not Found") })
                : Json(HttpStatusCode.OK, new JsonObject { ["customer"] = customer.DeepClone() });
        }

        if (method == HttpMethod.Post && path == "/customers.json")
        {
            var input = JsonNode.Parse(body!)!["customer"]!.AsObject();
            lock (_gate)
            {
                var reference = (string?)input["reference"];
                if (reference is not null && Customers.Values.Any(c => (string?)c["reference"] == reference))
                {
                    return Json(HttpStatusCode.UnprocessableEntity,
                        new JsonObject { ["errors"] = new JsonArray("Reference: must be unique - that value has been taken.") });
                }

                var id = ++_nextCustomerId;
                var customer = new JsonObject
                {
                    ["id"] = id,
                    ["first_name"] = (string?)input["first_name"],
                    ["last_name"] = (string?)input["last_name"],
                    ["email"] = (string?)input["email"],
                    ["reference"] = reference,
                };
                Customers[id] = customer;
                return Json(HttpStatusCode.Created, new JsonObject { ["customer"] = customer.DeepClone() });
            }
        }

        if (method == HttpMethod.Post && path == "/subscriptions.json")
        {
            var input = JsonNode.Parse(body!)!["subscription"]!.AsObject();
            var handle = (string?)input["product_handle"];
            var product = Products.FirstOrDefault(p => (string?)p["handle"] == handle);
            if (product is null)
            {
                return Json(HttpStatusCode.UnprocessableEntity, new JsonObject { ["errors"] = new JsonArray("Product must be specified.") });
            }

            var customerId = (int)input["customer_id"]!;
            lock (_gate)
            {
                var id = ++_nextSubscriptionId;
                var subscription = Subscription(id, (string?)input["reference"], product, Customers[customerId]);
                Subscriptions[id] = (customerId, subscription);
                return Json(HttpStatusCode.Created, new JsonObject { ["subscription"] = subscription.DeepClone() });
            }
        }

        if (method == HttpMethod.Get && path.StartsWith("/customers/") && path.EndsWith("/subscriptions.json"))
        {
            var customerId = int.Parse(path.Split('/')[2]);
            var items = Subscriptions.Values.Where(s => s.CustomerId == customerId)
                .Select(s => (JsonNode)new JsonObject { ["subscription"] = s.Subscription.DeepClone() }).ToArray();
            return Json(HttpStatusCode.OK, new JsonArray(items));
        }

        return Json(HttpStatusCode.NotFound, new JsonObject { ["errors"] = new JsonArray($"No fake route for {method} {path}") });
    }

    public static JsonObject Subscription(int id, string? reference, JsonObject product, JsonObject customer, string state = "active") => new()
    {
        ["id"] = id,
        ["state"] = state,
        ["reference"] = reference,
        ["product_price_in_cents"] = (long)product["price_in_cents"]!,
        ["currency"] = "USD",
        ["current_period_ends_at"] = "2026-11-05T12:00:00Z",
        ["next_assessment_at"] = "2026-11-05T12:00:00Z",
        ["created_at"] = "2026-10-05T12:00:00Z",
        ["activated_at"] = "2026-10-05T12:00:00Z",
        ["product"] = product.DeepClone(),
        ["customer"] = customer.DeepClone(),
    };

    public static HttpResponseMessage Json(HttpStatusCode status, JsonNode? node) =>
        new(status) { Content = new StringContent(node?.ToJsonString() ?? "null", Encoding.UTF8, "application/json") };

    public static HttpResponseMessage RawJson(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string? QueryValue(string query, string key) =>
        query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p[0] == key)
            .Select(p => p.Length > 1 ? p[1] : "")
            .FirstOrDefault();

    public static JsonDocument Parse(string? body) => JsonDocument.Parse(body ?? "null");
}
