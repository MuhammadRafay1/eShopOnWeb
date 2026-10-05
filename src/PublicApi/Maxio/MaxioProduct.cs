using System;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.Maxio;

/// <summary>
/// Maxio "Product" schema (components/schemas/Product.yaml) - a subscription plan.
/// JSON property casing follows the spec (snake_case).
/// </summary>
public class MaxioProduct
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Handle { get; set; }

    public string? Description { get; set; }

    [JsonPropertyName("price_in_cents")]
    public int PriceInCents { get; set; }

    public int Interval { get; set; }

    [JsonPropertyName("interval_unit")]
    public string? IntervalUnit { get; set; }

    [JsonPropertyName("initial_charge_in_cents")]
    public int? InitialChargeInCents { get; set; }

    [JsonPropertyName("trial_price_in_cents")]
    public int? TrialPriceInCents { get; set; }

    [JsonPropertyName("trial_interval")]
    public int? TrialInterval { get; set; }

    [JsonPropertyName("trial_interval_unit")]
    public string? TrialIntervalUnit { get; set; }

    [JsonPropertyName("expiration_interval")]
    public int? ExpirationInterval { get; set; }

    [JsonPropertyName("expiration_interval_unit")]
    public string? ExpirationIntervalUnit { get; set; }

    [JsonPropertyName("require_credit_card")]
    public bool RequireCreditCard { get; set; }

    public bool Taxable { get; set; }

    [JsonPropertyName("archived_at")]
    public DateTimeOffset? ArchivedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("product_family")]
    public MaxioProductFamily? ProductFamily { get; set; }
}

/// <summary>
/// Maxio "Product Family" schema (components/schemas/Product-Family.yaml).
/// </summary>
public class MaxioProductFamily
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Handle { get; set; }

    public string? Description { get; set; }
}

/// <summary>
/// Maxio "Customer" schema (components/schemas/Customer.yaml) - the subset used by this app.
/// </summary>
public class MaxioCustomer
{
    public int Id { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Reference { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}