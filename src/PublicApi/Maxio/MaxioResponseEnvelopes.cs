namespace Microsoft.eShopWeb.PublicApi.Maxio;

/// <summary>
/// Response envelope for a single product, per components/schemas/Product-Response.yaml.
/// </summary>
public class MaxioProductResponse
{
    public MaxioProduct Product { get; set; } = new();
}

/// <summary>
/// Response envelope for a single customer, per components/schemas/Customer-Response.yaml.
/// </summary>
public class MaxioCustomerResponse
{
    public MaxioCustomer Customer { get; set; } = new();
}

/// <summary>
/// Response envelope for a single subscription, per components/schemas/Subscription-Response.yaml.
/// </summary>
public class MaxioSubscriptionResponse
{
    public MaxioSubscription Subscription { get; set; } = new();
}