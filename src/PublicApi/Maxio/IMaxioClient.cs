using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.PublicApi.Maxio;

/// <summary>
/// Typed client for the Maxio Advanced Billing API, built strictly against the OpenAPI
/// specification in maxio-spec/. Only the operations needed by the subscription
/// capability are exposed.
/// </summary>
public interface IMaxioClient
{
    /// <summary>
    /// GET /product_families/{product_family_id}/products.json -
    /// lists the subscription plans belonging to the configured product family.
    /// </summary>
    Task<IReadOnlyList<MaxioProduct>> ListFamilyProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /products/handle/{api_handle}.json - reads a plan by its handle. Returns null on 404.
    /// </summary>
    Task<MaxioProduct?> ReadProductByHandleAsync(string handle, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /customers/lookup.json?reference=... - reads a customer by its unique reference. Returns null on 404.
    /// </summary>
    Task<MaxioCustomer?> LookupCustomerByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /customers.json - creates a customer.
    /// </summary>
    Task<MaxioCustomer> CreateCustomerAsync(CreateMaxioCustomerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /customers/{customer_id}/subscriptions.json - lists the subscriptions that belong to a customer.
    /// </summary>
    Task<IReadOnlyList<MaxioSubscription>> ListCustomerSubscriptionsAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /subscriptions.json - creates a subscription for a customer and product.
    /// </summary>
    Task<MaxioSubscription> CreateSubscriptionAsync(CreateMaxioSubscriptionRequest request, CancellationToken cancellationToken = default);
}