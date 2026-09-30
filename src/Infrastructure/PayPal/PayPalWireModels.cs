using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

// Internal wire models mapping directly to the shapes defined by the PayPal OpenAPI specs
// (checkout_orders_v2, payments_payment_v2, vault_payment_tokens_v3, transaction_search_v1).
// Only the fields this integration reads or sends are modelled. Null fields are omitted on send.

internal class Money
{
    [JsonPropertyName("currency_code")] public string? CurrencyCode { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }
}

// ---- Token ----
internal class TokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("token_type")] public string? TokenType { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}

// ---- Error (shared error schema) ----
internal class PayPalErrorBody
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("debug_id")] public string? DebugId { get; set; }
    [JsonPropertyName("details")] public List<PayPalErrorDetail>? Details { get; set; }
}

internal class PayPalErrorDetail
{
    [JsonPropertyName("field")] public string? Field { get; set; }
    [JsonPropertyName("issue")] public string? Issue { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

// ---- Checkout Orders v2 ----
internal class CreateOrderRequest
{
    [JsonPropertyName("intent")] public string Intent { get; set; } = "AUTHORIZE";
    [JsonPropertyName("purchase_units")] public List<PurchaseUnitRequest> PurchaseUnits { get; set; } = new();
    [JsonPropertyName("payment_source")] public PaymentSourceRequest? PaymentSource { get; set; }
}

internal class PurchaseUnitRequest
{
    [JsonPropertyName("amount")] public Money? Amount { get; set; }
    [JsonPropertyName("custom_id")] public string? CustomId { get; set; }
    [JsonPropertyName("invoice_id")] public string? InvoiceId { get; set; }
}

internal class PaymentSourceRequest
{
    [JsonPropertyName("card")] public CardRequest? Card { get; set; }
}

internal class CardRequest
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("number")] public string? Number { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
    [JsonPropertyName("security_code")] public string? SecurityCode { get; set; }
    [JsonPropertyName("billing_address")] public CardAddress? BillingAddress { get; set; }
    [JsonPropertyName("vault_id")] public string? VaultId { get; set; }
}

internal class CardAddress
{
    [JsonPropertyName("address_line_1")] public string? AddressLine1 { get; set; }
    [JsonPropertyName("address_line_2")] public string? AddressLine2 { get; set; }
    [JsonPropertyName("admin_area_2")] public string? AdminArea2 { get; set; }
    [JsonPropertyName("admin_area_1")] public string? AdminArea1 { get; set; }
    [JsonPropertyName("postal_code")] public string? PostalCode { get; set; }
    [JsonPropertyName("country_code")] public string? CountryCode { get; set; }
}

internal class OrderResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("purchase_units")] public List<PurchaseUnitResponse>? PurchaseUnits { get; set; }
}

internal class PurchaseUnitResponse
{
    [JsonPropertyName("payments")] public PaymentCollection? Payments { get; set; }
}

internal class PaymentCollection
{
    [JsonPropertyName("authorizations")] public List<AuthorizationResponse>? Authorizations { get; set; }
    [JsonPropertyName("captures")] public List<CaptureResponse>? Captures { get; set; }
}

// ---- Payments v2 ----
internal class AuthorizationResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("amount")] public Money? Amount { get; set; }
    [JsonPropertyName("expiration_time")] public string? ExpirationTime { get; set; }
}

internal class CaptureResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("amount")] public Money? Amount { get; set; }
    [JsonPropertyName("seller_receivable_breakdown")] public SellerReceivableBreakdown? SellerReceivableBreakdown { get; set; }
    [JsonPropertyName("create_time")] public string? CreateTime { get; set; }
    [JsonPropertyName("update_time")] public string? UpdateTime { get; set; }
}

internal class SellerReceivableBreakdown
{
    [JsonPropertyName("gross_amount")] public Money? GrossAmount { get; set; }
    [JsonPropertyName("paypal_fee")] public Money? PayPalFee { get; set; }
    [JsonPropertyName("net_amount")] public Money? NetAmount { get; set; }
}

internal class CaptureRequest
{
    [JsonPropertyName("amount")] public Money? Amount { get; set; }
    [JsonPropertyName("final_capture")] public bool FinalCapture { get; set; }
}

internal class ReauthorizeRequest
{
    [JsonPropertyName("amount")] public Money? Amount { get; set; }
}

internal class RefundRequest
{
    [JsonPropertyName("amount")] public Money? Amount { get; set; }
    [JsonPropertyName("custom_id")] public string? CustomId { get; set; }
}

internal class RefundResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("amount")] public Money? Amount { get; set; }
}

// ---- Vault v3 ----
internal class VaultTokenRequest
{
    [JsonPropertyName("customer")] public VaultCustomer? Customer { get; set; }
    [JsonPropertyName("payment_source")] public VaultPaymentSource PaymentSource { get; set; } = new();
}

internal class VaultCustomer
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("merchant_customer_id")] public string? MerchantCustomerId { get; set; }
}

internal class VaultPaymentSource
{
    [JsonPropertyName("card")] public VaultCard? Card { get; set; }
}

internal class VaultCard
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("number")] public string? Number { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
    [JsonPropertyName("security_code")] public string? SecurityCode { get; set; }
    [JsonPropertyName("billing_address")] public CardAddress? BillingAddress { get; set; }
}

internal class VaultTokenResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("customer")] public VaultCustomer? Customer { get; set; }
    [JsonPropertyName("payment_source")] public VaultTokenResponseSource? PaymentSource { get; set; }
}

internal class VaultTokenResponseSource
{
    [JsonPropertyName("card")] public VaultCardResponse? Card { get; set; }
}

internal class VaultCardResponse
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("last_digits")] public string? LastDigits { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
}

internal class VaultTokenListResponse
{
    [JsonPropertyName("total_items")] public int TotalItems { get; set; }
    [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
    [JsonPropertyName("payment_tokens")] public List<VaultTokenResponse>? PaymentTokens { get; set; }
}

// ---- Transaction Search v1 ----
internal class TransactionSearchResponse
{
    [JsonPropertyName("transaction_details")] public List<TransactionDetail>? TransactionDetails { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("total_items")] public int TotalItems { get; set; }
    [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
}

internal class TransactionDetail
{
    [JsonPropertyName("transaction_info")] public TransactionInfo? TransactionInfo { get; set; }
}

internal class TransactionInfo
{
    [JsonPropertyName("transaction_id")] public string? TransactionId { get; set; }
    [JsonPropertyName("paypal_reference_id")] public string? PayPalReferenceId { get; set; }
    [JsonPropertyName("paypal_reference_id_type")] public string? PayPalReferenceIdType { get; set; }
    [JsonPropertyName("transaction_event_code")] public string? TransactionEventCode { get; set; }
    [JsonPropertyName("transaction_status")] public string? TransactionStatus { get; set; }
    [JsonPropertyName("transaction_amount")] public Money? TransactionAmount { get; set; }
    [JsonPropertyName("fee_amount")] public Money? FeeAmount { get; set; }
    [JsonPropertyName("invoice_id")] public string? InvoiceId { get; set; }
    [JsonPropertyName("custom_field")] public string? CustomField { get; set; }
    [JsonPropertyName("transaction_initiation_date")] public string? TransactionInitiationDate { get; set; }
    [JsonPropertyName("transaction_updated_date")] public string? TransactionUpdatedDate { get; set; }
}
