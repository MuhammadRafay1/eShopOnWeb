using System;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.Billing.MaxioApi;

/// <summary>Serializable JSON envelopes and payloads for the Maxio Billing API.</summary>
public sealed record MaxioProductEnvelope(
    [property: JsonPropertyName("product")] MaxioProduct? Product);

public sealed record MaxioCustomerEnvelope(
    [property: JsonPropertyName("customer")] MaxioCustomer? Customer);

public sealed record MaxioSubscriptionEnvelope(
    [property: JsonPropertyName("subscription")] MaxioSubscription? Subscription);

public sealed record MaxioSiteEnvelope(
    [property: JsonPropertyName("site")] MaxioSite? Site);

public sealed record MaxioSite(
    [property: JsonPropertyName("currency")] string? Currency);

public sealed record MaxioProductFamily(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("handle")] string? Handle);

public sealed record MaxioProduct(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("handle")] string? Handle,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("price_in_cents")] long? PriceInCents,
    [property: JsonPropertyName("interval")] int? Interval,
    [property: JsonPropertyName("interval_unit")] string? IntervalUnit,
    [property: JsonPropertyName("archived_at")] DateTimeOffset? ArchivedAt,
    [property: JsonPropertyName("require_credit_card")] bool? RequireCreditCard,
    [property: JsonPropertyName("product_family")] MaxioProductFamily? ProductFamily);

public sealed record MaxioCustomer(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("reference")] string? Reference);

public sealed record MaxioSubscription(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("balance_in_cents")] long? BalanceInCents,
    [property: JsonPropertyName("current_period_started_at")] DateTimeOffset? CurrentPeriodStartedAt,
    [property: JsonPropertyName("current_period_ends_at")] DateTimeOffset? CurrentPeriodEndsAt,
    [property: JsonPropertyName("next_assessment_at")] DateTimeOffset? NextAssessmentAt,
    [property: JsonPropertyName("activated_at")] DateTimeOffset? ActivatedAt,
    [property: JsonPropertyName("canceled_at")] DateTimeOffset? CanceledAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("payment_collection_method")] string? PaymentCollectionMethod,
    [property: JsonPropertyName("product_price_in_cents")] long? ProductPriceInCents,
    [property: JsonPropertyName("product")] MaxioProduct? Product,
    [property: JsonPropertyName("customer")] MaxioCustomer? Customer);

public sealed record MaxioCreateCustomerBody(
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("reference")] string Reference);

public sealed record MaxioCreateCustomerPayload(
    [property: JsonPropertyName("customer")] MaxioCreateCustomerBody Customer,
    [property: JsonPropertyName("uniqueness_token")] string UniquenessToken);

public sealed record MaxioCreateSubscriptionBody(
    [property: JsonPropertyName("product_handle")] string ProductHandle,
    [property: JsonPropertyName("customer_id")] long CustomerId,
    [property: JsonPropertyName("payment_collection_method")] string PaymentCollectionMethod);

public sealed record MaxioCreateSubscriptionPayload(
    [property: JsonPropertyName("subscription")] MaxioCreateSubscriptionBody Subscription,
    [property: JsonPropertyName("uniqueness_token")] string UniquenessToken);

/// <summary>A call that was attempted but rejected by the API. Carries the raw
/// HTTP status and error body so callers can decide how to recover.</summary>
public sealed record MaxioCallResult<T>(
    int StatusCode,
    T? Value,
    string? ErrorBody)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}