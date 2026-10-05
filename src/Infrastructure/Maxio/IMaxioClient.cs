using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Maxio;

/// <summary>
/// Strongly typed access to the Maxio Advanced Billing REST API endpoints
/// used by this integration. All methods return raw Maxio models.
/// </summary>
public interface IMaxioClient
{
    /// <summary>
    /// Reads a product family by its stable handle, e.g. GET /product_families/handle:{handle}.json.
    /// Returns null when the family does not exist.
    /// </summary>
    Task<Maxio.Models.MaxioProductFamily?> GetProductFamilyByHandleAsync(string handle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the products (subscription plans) belonging to a product family, following pagination.
    /// </summary>
    Task<IReadOnlyList<Maxio.Models.MaxioProduct>> ListProductsAsync(int productFamilyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up a customer by its unique reference, e.g. GET /customers/lookup.json?reference=...
    /// Returns null when no customer with that reference exists.
    /// </summary>
    Task<Maxio.Models.MaxioCustomer?> GetCustomerByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a customer via POST /customers.json. Throws <see cref="MaxioApiException"/> with
    /// status 422 when the reference is already taken (or another validation error occurs).
    /// </summary>
    Task<Maxio.Models.MaxioCustomer> CreateCustomerAsync(string firstName, string lastName, string email, string reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the subscriptions belonging to a customer, following pagination.
    /// </summary>
    Task<IReadOnlyList<Maxio.Models.MaxioSubscription>> ListCustomerSubscriptionsAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a subscription via POST /subscriptions.json for the given customer and product handle.
    /// </summary>
    Task<Maxio.Models.MaxioSubscription> CreateSubscriptionAsync(int customerId, string productHandle, CancellationToken cancellationToken = default);
}