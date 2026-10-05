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

namespace Microsoft.eShopWeb.PublicApi.Maxio;

/// <summary>
/// HttpClient-based implementation of <see cref="IMaxioClient"/>. Every interaction is
/// built against the OpenAPI specification in maxio-spec/: auth (BasicAuth - api key as
/// username, "x" as password), server templating, paths, query parameters, request and
/// response schemas, and error models.
/// </summary>
public class MaxioClient : IMaxioClient
{
    private const int PerPage = 100;
    private const int MaxPages = 20;
    private const string FamilyHandlePrefix = "handle:";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly MaxioOptions _options;

    public MaxioClient(HttpClient httpClient, IOptions<MaxioOptions> options)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_options.GetBaseAddress());
    }

    public async Task<IReadOnlyList<MaxioProduct>> ListFamilyProductsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<MaxioProduct>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"/product_families/{Uri.EscapeDataString(FamilyHandlePrefix + _options.ProductFamilyHandle)}/products.json?page={page}&per_page={PerPage}";
            var batch = await GetAsync<List<MaxioProductResponse>>(url, cancellationToken);
            if (batch is null)
            {
                break;
            }

            results.AddRange(batch.Select(b => b.Product));
            if (batch.Count < PerPage)
            {
                break;
            }
        }
        return results;
    }

    public Task<MaxioProduct?> ReadProductByHandleAsync(string handle, CancellationToken cancellationToken = default)
    {
        var url = $"/products/handle/{Uri.EscapeDataString(handle)}.json";
        return GetOrNullAsync<MaxioProductResponse, MaxioProduct?>(url, r => r.Product, cancellationToken);
    }

    public Task<MaxioCustomer?> LookupCustomerByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var url = $"/customers/lookup.json?reference={Uri.EscapeDataString(reference)}";
        return GetOrNullAsync<MaxioCustomerResponse, MaxioCustomer?>(url, r => r.Customer, cancellationToken);
    }

    public async Task<MaxioCustomer> CreateCustomerAsync(CreateMaxioCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<CreateMaxioCustomerRequest, MaxioCustomerResponse>("/customers.json", request, cancellationToken);
        return result!.Customer;
    }

    public async Task<IReadOnlyList<MaxioSubscription>> ListCustomerSubscriptionsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var results = new List<MaxioSubscription>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"/customers/{customerId}/subscriptions.json?page={page}&per_page={PerPage}";
            var batch = await GetAsync<List<MaxioSubscriptionResponse>>(url, cancellationToken);
            if (batch is null)
            {
                break;
            }

            results.AddRange(batch.Select(b => b.Subscription));
            if (batch.Count < PerPage)
            {
                break;
            }
        }
        return results;
    }

    public async Task<MaxioSubscription> CreateSubscriptionAsync(CreateMaxioSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<CreateMaxioSubscriptionRequest, MaxioSubscriptionResponse>("/subscriptions.json", request, cancellationToken);
        return result!.Subscription;
    }

    private async Task<T?> GetAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, requestUri);
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task<T?> GetOrNullAsync<TEnvelope, T>(string requestUri, Func<TEnvelope, T> unwrap, CancellationToken cancellationToken)
        where TEnvelope : class
    {
        try
        {
            var envelope = await GetAsync<TEnvelope>(requestUri, cancellationToken);
            return envelope is null ? default : unwrap(envelope);
        }
        catch (MaxioApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string requestUri, TRequest body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, requestUri);
        request.Content = JsonContent.Create(body, options: s_jsonOptions);
        return await SendAsync<TResponse>(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiKey}:x")));
        return request;
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new MaxioApiException((int)response.StatusCode, MaxioErrorResponse.Parse(body));
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, s_jsonOptions);
        }
        catch (JsonException ex)
        {
            var preview = body.Length > 300 ? body[..300] : body;
            throw new InvalidOperationException(
                $"Failed to deserialize Maxio response from '{request.RequestUri}' " +
                $"(content-type: {response.Content.Headers.ContentType}): {ex.Message} | body starts with: {preview}", ex);
        }
    }
}