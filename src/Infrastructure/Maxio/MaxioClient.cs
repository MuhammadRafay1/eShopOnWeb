using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.eShopWeb.Infrastructure.Maxio.Models;

namespace Microsoft.eShopWeb.Infrastructure.Maxio;

/// <summary>
/// Thrown when the Maxio Advanced Billing API answers with a non-success status code.
/// Carries the raw response body for diagnostics.
/// </summary>
public class MaxioApiException : Exception
{
    public MaxioApiException(HttpStatusCode statusCode, string body)
        : base($"The Maxio Advanced Billing API returned status {(int)statusCode}. Response body: {body}")
    {
        StatusCode = statusCode;
        Body = body;
    }

    public MaxioApiException(HttpStatusCode statusCode, string body, Exception innerException)
        : base($"The Maxio Advanced Billing API returned an unexpected response for status {(int)statusCode}. Response body: {body}", innerException)
    {
        StatusCode = statusCode;
        Body = body;
    }

    public HttpStatusCode StatusCode { get; }

    public string Body { get; }
}

/// <summary>
/// Typed HTTP client for the Maxio Advanced Billing REST API.
/// Contract verified against the official Maxio developer portal / ab-dotnet-sdk:
///  - Base URL: https://{subdomain}.chargify.com (US), overridable via Maxio:BaseUrl.
///  - Authentication: Basic with the API key as the user name.
///  - Endpoints return JSON with snake_case fields; lists are wrapped in envelopes
///    (e.g. {"customer":{...}}) and support page/per_page pagination.
/// </summary>
public class MaxioClient : IMaxioClient
{
    private const int PageSize = 50;
    private const int MaxPages = 20;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly MaxioOptions _options;

    public MaxioClient(HttpClient httpClient, IOptions<MaxioOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(BuildBaseUrl(_options));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ApiKey}:x")));
    }

    public static string BuildBaseUrl(MaxioOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return options.BaseUrl.TrimEnd('/');
        }

        return $"https://{options.Subdomain.Trim()}.chargify.com";
    }

    public async Task<MaxioProductFamily?> GetProductFamilyByHandleAsync(string handle, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"product_families/handle:{Uri.EscapeDataString(handle)}.json", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var envelope = await ReadAsync<MaxioProductFamilyResponse>(response, cancellationToken);
        return envelope.ProductFamily;
    }

    public async Task<IReadOnlyList<MaxioProduct>> ListProductsAsync(int productFamilyId, CancellationToken cancellationToken = default)
    {
        var results = new List<MaxioProduct>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await _httpClient.GetAsync(
                $"product_families/{productFamilyId}/products.json?page={page}&per_page={PageSize}", cancellationToken);
            var pageItems = await ReadAsync<List<MaxioProductResponse>>(response, cancellationToken);

            results.AddRange(pageItems.Select(envelope => envelope.Product));

            if (pageItems.Count < PageSize)
            {
                break;
            }
        }
        return results;
    }

    public async Task<MaxioCustomer?> GetCustomerByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"customers/lookup.json?reference={Uri.EscapeDataString(reference)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var envelope = await ReadAsync<MaxioCustomerResponse>(response, cancellationToken);
        return envelope.Customer;
    }

    public async Task<MaxioCustomer> CreateCustomerAsync(string firstName, string lastName, string email, string reference, CancellationToken cancellationToken = default)
    {
        var request = new MaxioCreateCustomerRequest
        {
            Customer = new MaxioCreateCustomerBody
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Reference = reference
            }
        };

        var response = await _httpClient.PostAsJsonAsync("customers.json", request, _jsonOptions, cancellationToken);
        var envelope = await ReadAsync<MaxioCustomerResponse>(response, cancellationToken);
        return envelope.Customer;
    }

    public async Task<IReadOnlyList<MaxioSubscription>> ListCustomerSubscriptionsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var results = new List<MaxioSubscription>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var response = await _httpClient.GetAsync(
                $"customers/{customerId}/subscriptions.json?page={page}&per_page={PageSize}", cancellationToken);
            var pageItems = await ReadAsync<List<MaxioSubscriptionResponse>>(response, cancellationToken);

            results.AddRange(pageItems.Select(envelope => envelope.Subscription));

            if (pageItems.Count < PageSize)
            {
                break;
            }
        }
        return results;
    }

    public async Task<MaxioSubscription> CreateSubscriptionAsync(int customerId, string productHandle, CancellationToken cancellationToken = default)
    {
        var request = new MaxioCreateSubscriptionRequest
        {
            Subscription = new MaxioCreateSubscriptionBody
            {
                ProductHandle = productHandle,
                CustomerId = customerId,
                // eShop never captures a card, so subscriptions must be collected by
                // invoice (remittance) rather than charged automatically at signup.
                PaymentCollectionMethod = "remittance"
            }
        };

        var response = await _httpClient.PostAsJsonAsync("subscriptions.json", request, _jsonOptions, cancellationToken);
        var envelope = await ReadAsync<MaxioSubscriptionResponse>(response, cancellationToken);
        return envelope.Subscription;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new MaxioApiException(response.StatusCode, body);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, _jsonOptions)!;
        }
        catch (JsonException ex)
        {
            throw new MaxioApiException(response.StatusCode, $"Unable to parse response as {typeof(T).Name}: {body}", ex);
        }
    }
}