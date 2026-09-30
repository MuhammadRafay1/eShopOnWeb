using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

// Wire-format request DTOs for PayPal's REST APIs. Property names follow PayPal's snake_case exactly
// via JsonPropertyName; responses are read with JsonDocument rather than typed DTOs (see PayPalGateway).

internal record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

internal record CreateOrderRequestDto(
    [property: JsonPropertyName("intent")] string Intent,
    [property: JsonPropertyName("purchase_units")] PurchaseUnitRequestDto[] PurchaseUnits,
    [property: JsonPropertyName("payment_source")] PaymentSourceRequestDto PaymentSource);

internal record PurchaseUnitRequestDto(
    [property: JsonPropertyName("reference_id")] string ReferenceId,
    [property: JsonPropertyName("custom_id")] string CustomId,
    [property: JsonPropertyName("invoice_id")] string InvoiceId,
    [property: JsonPropertyName("amount")] AmountDto Amount);

internal record AmountDto(
    [property: JsonPropertyName("currency_code")] string CurrencyCode,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("breakdown")] AmountBreakdownDto? Breakdown = null);

internal record AmountBreakdownDto(
    [property: JsonPropertyName("item_total")] AmountDto ItemTotal);

internal record PaymentSourceRequestDto(
    [property: JsonPropertyName("card")] CardRequestDto Card);

internal record CardRequestDto(
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("expiry")] string? Expiry,
    [property: JsonPropertyName("security_code")] string? SecurityCode,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("billing_address")] BillingAddressRequestDto? BillingAddress,
    [property: JsonPropertyName("vault_id")] string? VaultId,
    [property: JsonPropertyName("attributes")] CardAttributesRequestDto? Attributes);

internal record BillingAddressRequestDto(
    [property: JsonPropertyName("address_line_1")] string AddressLine1,
    [property: JsonPropertyName("admin_area_2")] string AdminArea2,
    [property: JsonPropertyName("admin_area_1")] string? AdminArea1,
    [property: JsonPropertyName("postal_code")] string PostalCode,
    [property: JsonPropertyName("country_code")] string CountryCode);

internal record CardAttributesRequestDto(
    [property: JsonPropertyName("verification")] VerificationRequestDto Verification);

internal record VerificationRequestDto(
    [property: JsonPropertyName("method")] string Method);

internal record CaptureRequestDto(
    [property: JsonPropertyName("final_capture")] bool FinalCapture);

internal record ReauthorizeRequestDto(
    [property: JsonPropertyName("amount")] AmountDto Amount);

internal record RefundRequestDto(
    [property: JsonPropertyName("amount")] AmountDto Amount);

internal record PaymentTokenRequestDto(
    [property: JsonPropertyName("payment_source")] PaymentSourceTokenRequestDto PaymentSource);

internal record PaymentSourceTokenRequestDto(
    [property: JsonPropertyName("token")] TokenRefDto Token);

internal record TokenRefDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type);
