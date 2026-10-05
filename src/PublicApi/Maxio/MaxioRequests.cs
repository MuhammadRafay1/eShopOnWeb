using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.Maxio;

/// <summary>
/// Request body for POST /customers.json (components/schemas/Create-Customer-Request.yaml).
/// </summary>
public class CreateMaxioCustomerRequest
{
    [JsonPropertyName("customer")]
    public CustomerAttributes Customer { get; set; } = new();

    public class CustomerAttributes
    {
        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("last_name")]
        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        /// <summary>Unique identifier from this app; must be unique per site.</summary>
        public string? Reference { get; set; }
    }
}

/// <summary>
/// Request body for POST /subscriptions.json (components/schemas/Create-Subscription-Request.yaml).
/// </summary>
public class CreateMaxioSubscriptionRequest
{
    [JsonPropertyName("subscription")]
    public SubscriptionAttributes Subscription { get; set; } = new();

    public class SubscriptionAttributes
    {
        [JsonPropertyName("product_handle")]
        public string? ProductHandle { get; set; }

        [JsonPropertyName("customer_id")]
        public int? CustomerId { get; set; }

        /// <summary>automatic | remittance (components/schemas/Collection-Method.yaml).</summary>
        [JsonPropertyName("payment_collection_method")]
        public string? PaymentCollectionMethod { get; set; }

        /// <summary>The reference value (provided by this app) for the subscription itself.</summary>
        public string? Reference { get; set; }
    }
}