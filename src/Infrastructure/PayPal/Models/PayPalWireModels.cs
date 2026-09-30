using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal.Models;

// Wire-format DTOs mirroring the JSON shapes declared in api-specs/paypal (snake_case).
// These are internal to the PayPal HTTP client - ApplicationCore only ever sees the plain
// result/input types declared in ApplicationCore/Interfaces/PayPalClientModels.cs.

internal class MoneyDto
{
    [JsonPropertyName("currency_code")] public string CurrencyCode { get; set; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
}

internal class BillingAddressDto
{
    [JsonPropertyName("address_line_1")] public string? AddressLine1 { get; set; }
    [JsonPropertyName("admin_area_1")] public string? AdminArea1 { get; set; }
    [JsonPropertyName("admin_area_2")] public string? AdminArea2 { get; set; }
    [JsonPropertyName("postal_code")] public string? PostalCode { get; set; }
    [JsonPropertyName("country_code")] public string? CountryCode { get; set; }
}

internal class CustomerDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("merchant_customer_id")] public string? MerchantCustomerId { get; set; }
}

internal class VaultInstructionDto
{
    [JsonPropertyName("store_in_vault")] public string StoreInVault { get; set; } = "ON_SUCCESS";
}

internal class CardAttributesDto
{
    [JsonPropertyName("customer")] public CustomerDto? Customer { get; set; }
    [JsonPropertyName("vault")] public VaultInstructionDto? Vault { get; set; }
}

internal class CardRequestDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("number")] public string? Number { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
    [JsonPropertyName("security_code")] public string? SecurityCode { get; set; }
    [JsonPropertyName("billing_address")] public BillingAddressDto? BillingAddress { get; set; }
    [JsonPropertyName("attributes")] public CardAttributesDto? Attributes { get; set; }
    [JsonPropertyName("vault_id")] public string? VaultId { get; set; }
}

internal class PaymentSourceRequestDto
{
    [JsonPropertyName("card")] public CardRequestDto? Card { get; set; }
}

internal class PurchaseUnitRequestDto
{
    [JsonPropertyName("invoice_id")] public string? InvoiceId { get; set; }
    [JsonPropertyName("custom_id")] public string? CustomId { get; set; }
    [JsonPropertyName("amount")] public MoneyDto Amount { get; set; } = new();
}

internal class CreateOrderRequestDto
{
    [JsonPropertyName("intent")] public string Intent { get; set; } = "AUTHORIZE";
    [JsonPropertyName("purchase_units")] public List<PurchaseUnitRequestDto> PurchaseUnits { get; set; } = new();
    [JsonPropertyName("payment_source")] public PaymentSourceRequestDto? PaymentSource { get; set; }
}

internal class CardVaultResponseDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("customer")] public CustomerDto? Customer { get; set; }
}

internal class CardAttributesResponseDto
{
    [JsonPropertyName("vault")] public CardVaultResponseDto? Vault { get; set; }
}

internal class CardResponseDto
{
    [JsonPropertyName("last_digits")] public string? LastDigits { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
    [JsonPropertyName("attributes")] public CardAttributesResponseDto? Attributes { get; set; }
}

internal class PaymentSourceResponseDto
{
    [JsonPropertyName("card")] public CardResponseDto? Card { get; set; }
}

internal class AuthorizationDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public MoneyDto? Amount { get; set; }
    [JsonPropertyName("expiration_time")] public DateTimeOffset? ExpirationTime { get; set; }
}

internal class SellerReceivableBreakdownDto
{
    [JsonPropertyName("gross_amount")] public MoneyDto? GrossAmount { get; set; }
    [JsonPropertyName("paypal_fee")] public MoneyDto? PaypalFee { get; set; }
    [JsonPropertyName("net_amount")] public MoneyDto? NetAmount { get; set; }
}

internal class CaptureDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public MoneyDto? Amount { get; set; }
    [JsonPropertyName("seller_receivable_breakdown")] public SellerReceivableBreakdownDto? SellerReceivableBreakdown { get; set; }
}

internal class PaymentCollectionDto
{
    [JsonPropertyName("authorizations")] public List<AuthorizationDto>? Authorizations { get; set; }
    [JsonPropertyName("captures")] public List<CaptureDto>? Captures { get; set; }
}

internal class PurchaseUnitResponseDto
{
    [JsonPropertyName("payments")] public PaymentCollectionDto? Payments { get; set; }
}

internal class LinkDto
{
    [JsonPropertyName("rel")] public string? Rel { get; set; }
    [JsonPropertyName("href")] public string? Href { get; set; }
}

internal class OrderResponseDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("payment_source")] public PaymentSourceResponseDto? PaymentSource { get; set; }
    [JsonPropertyName("purchase_units")] public List<PurchaseUnitResponseDto>? PurchaseUnits { get; set; }
    [JsonPropertyName("links")] public List<LinkDto>? Links { get; set; }
}

internal class CaptureRequestDto
{
    [JsonPropertyName("amount")] public MoneyDto Amount { get; set; } = new();
    [JsonPropertyName("final_capture")] public bool FinalCapture { get; set; } = true;
}

internal class ReauthorizeRequestDto
{
    [JsonPropertyName("amount")] public MoneyDto Amount { get; set; } = new();
}

internal class RefundRequestDto
{
    [JsonPropertyName("amount")] public MoneyDto? Amount { get; set; }
}

internal class RefundResponseDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public MoneyDto? Amount { get; set; }
}

internal class VaultPaymentSourceDto
{
    [JsonPropertyName("card")] public CardRequestDto Card { get; set; } = new();
}

internal class VaultCreateRequestDto
{
    [JsonPropertyName("customer")] public CustomerDto? Customer { get; set; }
    [JsonPropertyName("payment_source")] public VaultPaymentSourceDto PaymentSource { get; set; } = new();
}

internal class VaultResponseDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("customer")] public CustomerDto? Customer { get; set; }
    [JsonPropertyName("payment_source")] public PaymentSourceResponseDto? PaymentSource { get; set; }
}

internal class TransactionInfoDto
{
    [JsonPropertyName("transaction_id")] public string TransactionId { get; set; } = string.Empty;
    [JsonPropertyName("transaction_status")] public string? TransactionStatus { get; set; }
    [JsonPropertyName("transaction_initiation_date")] public DateTimeOffset? TransactionInitiationDate { get; set; }
    [JsonPropertyName("transaction_amount")] public MoneyDto? TransactionAmount { get; set; }
    [JsonPropertyName("fee_amount")] public MoneyDto? FeeAmount { get; set; }
    [JsonPropertyName("invoice_id")] public string? InvoiceId { get; set; }
    [JsonPropertyName("custom_field")] public string? CustomField { get; set; }
}

internal class TransactionDetailDto
{
    [JsonPropertyName("transaction_info")] public TransactionInfoDto? TransactionInfo { get; set; }
}

internal class SearchResponseDto
{
    [JsonPropertyName("transaction_details")] public List<TransactionDetailDto>? TransactionDetails { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
    [JsonPropertyName("total_items")] public int TotalItems { get; set; }
}

internal class ErrorDetailDto
{
    [JsonPropertyName("issue")] public string? Issue { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

internal class ErrorResponseDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("debug_id")] public string? DebugId { get; set; }
    [JsonPropertyName("details")] public List<ErrorDetailDto>? Details { get; set; }
}

internal class OAuthTokenResponseDto
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}
