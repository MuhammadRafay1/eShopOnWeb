using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Billing.MaxioApi;

/// <summary>
/// Typed HTTP client for the Maxio Billing API (Advanced Billing). Authentication
/// is HTTP Basic over TLS with the API key as the username and "X" as the
/// password, per the Billing API authentication documentation. JSON endpoints
/// are addressed with the ".json" suffix.
/// </summary>
public sealed class MaxioApiClient
{
    private const int PerPageMax = 200;
    private const string BasicAuthPassword = "X";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly MaxioBillingOptions _options;

    public MaxioApiClient(HttpClient httpClient, IOptions<MaxioBillingOptions> options)
    {
        _options = options.Value;
        _options.Validate();

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_options.ResolveBaseUrl(), UriKind.Absolute);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiKey!.Trim()}:{BasicAuthPassword}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<IReadOnlyList<MaxioProduct>> ListProductsForFamilyAsync(string familyHandle, CancellationToken cancellationToken = default)
    {
        var path = $"product_families/handle:{Uri.EscapeDataString(familyHandle)}/products.json?per_page={PerPageMax}";
        var envelopes = await GetAsync<List<MaxioProductEnvelope>>(path, cancellationToken) ?? new List<MaxioProductEnvelope>();
        return envelopes
            .Where(e => e.Product is not null)
            .Select(e => e.Product!)
            .ToList();
    }

    public async Task<MaxioProduct?> LookupProductByHandleAsync(string productHandle, CancellationToken cancellationToken = default)
    {
        var path = $"products/handle/{Uri.EscapeDataString(productHandle)}.json";
        var envelope = await GetAsync<MaxioProductEnvelope>(path, cancellationToken, notFoundAsNull: true);
        return envelope?.Product;
    }

    public async Task<MaxioCustomer?> LookupCustomerByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var path = $"customers/lookup.json?reference={Uri.EscapeDataString(reference)}";
        var envelope = await GetAsync<MaxioCustomerEnvelope>(path, cancellationToken, notFoundAsNull: true);
        return envelope?.Customer;
    }

    /// <summary>
    /// Attempts to create a customer. Returns a non-success <see cref="MaxioCallResult{T}"/>
    /// (e.g. 422 for a duplicate reference) instead of throwing, so callers can
    /// implement lookup-then-create recovery.
    /// </summary>
    public async Task<MaxioCallResult<MaxioCustomerEnvelope>> TryCreateCustomerAsync(
        MaxioCreateCustomerBody customer, string uniquenessToken, CancellationToken cancellationToken = default)
    {
        var payload = new MaxioCreateCustomerPayload(customer, uniquenessToken);
        return await PostAsync<MaxioCreateCustomerPayload, MaxioCustomerEnvelope>("customers.json", payload, cancellationToken);
    }

    public async Task<IReadOnlyList<MaxioSubscription>> ListCustomerSubscriptionsAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var path = $"customers/{customerId}/subscriptions.json?per_page={PerPageMax}";
        var envelopes = await GetAsync<List<MaxioSubscriptionEnvelope>>(path, cancellationToken) ?? new List<MaxioSubscriptionEnvelope>();
        return envelopes
            .Where(e => e.Subscription is not null)
            .Select(e => e.Subscription!)
            .ToList();
    }

    /// <summary>
    /// Attempts to create a subscription. Returns a non-success <see cref="MaxioCallResult{T}"/>
    /// (e.g. 409 duplicate-prevention or 422 validation) instead of throwing,
    /// so callers can implement idempotent recovery.
    /// </summary>
    public async Task<MaxioCallResult<MaxioSubscriptionEnvelope>> TryCreateSubscriptionAsync(
        MaxioCreateSubscriptionPayload payload, CancellationToken cancellationToken = default)
    {
        return await PostAsync<MaxioCreateSubscriptionPayload, MaxioSubscriptionEnvelope>("subscriptions.json", payload, cancellationToken);
    }

    public async Task<string?> GetSiteCurrencyAsync(CancellationToken cancellationToken = default)
    {
        var envelope = await GetAsync<MaxioSiteEnvelope>("site.json", cancellationToken);
        return envelope?.Site?.Currency;
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken, bool notFoundAsNull = false)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (notFoundAsNull && response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    private async Task<MaxioCallResult<TResponse>> PostAsync<TRequest, TResponse>(
        string path, TRequest payload, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, payload, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return new MaxioCallResult<TResponse>((int)response.StatusCode, default, errorBody);
        }

        var value = await ReadAsync<TResponse>(response, cancellationToken);
        return new MaxioCallResult<TResponse>((int)response.StatusCode, value, null);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new MaxioApiException((int)response.StatusCode, body);
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }
}