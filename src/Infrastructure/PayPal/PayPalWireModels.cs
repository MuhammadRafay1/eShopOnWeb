using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

// Wire-format DTOs mirroring PayPal's JSON exactly (snake_case). Internal to the Infrastructure
// PayPal client - the rest of the app only ever sees Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments types.

internal class PpMoney
{
    [JsonPropertyName("currency_code")] public string CurrencyCode { get; set; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
}

internal class PpAddress
{
    [JsonPropertyName("country_code")] public string CountryCode { get; set; } = string.Empty;
    [JsonPropertyName("address_line_1")] public string? AddressLine1 { get; set; }
    [JsonPropertyName("admin_area_1")] public string? AdminArea1 { get; set; }
    [JsonPropertyName("admin_area_2")] public string? AdminArea2 { get; set; }
    [JsonPropertyName("postal_code")] public string? PostalCode { get; set; }
}

internal class PpLink
{
    [JsonPropertyName("href")] public string Href { get; set; } = string.Empty;
    [JsonPropertyName("rel")] public string Rel { get; set; } = string.Empty;
    [JsonPropertyName("method")] public string? Method { get; set; }
}

internal class PpErrorDetail
{
    [JsonPropertyName("issue")] public string? Issue { get; set; }
    [JsonPropertyName("field")] public string? Field { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

internal class PpError
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("debug_id")] public string? DebugId { get; set; }
    [JsonPropertyName("details")] public List<PpErrorDetail>? Details { get; set; }
}

// ---- Create order (/v2/checkout/orders) ----

internal class PpCreateOrderRequest
{
    [JsonPropertyName("intent")] public string Intent { get; set; } = "AUTHORIZE";
    [JsonPropertyName("purchase_units")] public List<PpPurchaseUnitRequest> PurchaseUnits { get; set; } = new();
    [JsonPropertyName("payment_source")] public PpPaymentSourceRequest? PaymentSource { get; set; }
}

internal class PpPurchaseUnitRequest
{
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("custom_id")] public string? CustomId { get; set; }
    [JsonPropertyName("invoice_id")] public string? InvoiceId { get; set; }
    [JsonPropertyName("amount")] public PpMoney Amount { get; set; } = new();
}

internal class PpPaymentSourceRequest
{
    [JsonPropertyName("card")] public PpCardRequest? Card { get; set; }
}

internal class PpCardRequest
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("number")] public string? Number { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
    [JsonPropertyName("security_code")] public string? SecurityCode { get; set; }
    [JsonPropertyName("billing_address")] public PpAddress? BillingAddress { get; set; }
    [JsonPropertyName("vault_id")] public string? VaultId { get; set; }
    [JsonPropertyName("attributes")] public PpCardAttributes? Attributes { get; set; }
}

internal class PpCardAttributes
{
    [JsonPropertyName("verification")] public PpCardVerification? Verification { get; set; }
}

internal class PpCardVerification
{
    [JsonPropertyName("method")] public string Method { get; set; } = "SCA_WHEN_REQUIRED";
}

internal class PpOrderResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("links")] public List<PpLink>? Links { get; set; }
    [JsonPropertyName("purchase_units")] public List<PpPurchaseUnitResponse>? PurchaseUnits { get; set; }
}

internal class PpPurchaseUnitResponse
{
    [JsonPropertyName("payments")] public PpPaymentCollection? Payments { get; set; }
}

internal class PpPaymentCollection
{
    [JsonPropertyName("authorizations")] public List<PpAuthorization>? Authorizations { get; set; }
    [JsonPropertyName("captures")] public List<PpCapture>? Captures { get; set; }
}

internal class PpAuthorization
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public PpMoney? Amount { get; set; }
    [JsonPropertyName("expiration_time")] public string? ExpirationTime { get; set; }
}

// ---- Authorize (/v2/checkout/orders/{id}/authorize) ----

internal class PpAuthorizeOrderRequest
{
    [JsonPropertyName("payment_source")] public PpPaymentSourceRequest? PaymentSource { get; set; }
}

// ---- Reauthorize / Capture / Void (/v2/payments/authorizations/{id}/...) ----

internal class PpAmountOnlyRequest
{
    [JsonPropertyName("amount")] public PpMoney? Amount { get; set; }
}

internal class PpCaptureRequest
{
    [JsonPropertyName("final_capture")] public bool FinalCapture { get; set; } = true;
}

internal class PpCapture
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public PpMoney? Amount { get; set; }
    [JsonPropertyName("seller_receivable_breakdown")] public PpSellerReceivableBreakdown? SellerReceivableBreakdown { get; set; }
}

internal class PpSellerReceivableBreakdown
{
    [JsonPropertyName("gross_amount")] public PpMoney? GrossAmount { get; set; }
    [JsonPropertyName("paypal_fee")] public PpMoney? PaypalFee { get; set; }
    [JsonPropertyName("net_amount")] public PpMoney? NetAmount { get; set; }
}

// ---- Refund (/v2/payments/captures/{id}/refund) ----

internal class PpRefundRequest
{
    [JsonPropertyName("amount")] public PpMoney? Amount { get; set; }
    [JsonPropertyName("custom_id")] public string? CustomId { get; set; }
    [JsonPropertyName("invoice_id")] public string? InvoiceId { get; set; }
}

internal class PpRefundResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public PpMoney? Amount { get; set; }
    [JsonPropertyName("seller_payable_breakdown")] public PpSellerPayableBreakdown? SellerPayableBreakdown { get; set; }
}

internal class PpSellerPayableBreakdown
{
    [JsonPropertyName("total_refunded_amount")] public PpMoney? TotalRefundedAmount { get; set; }
}

// ---- Vault (/v3/vault/payment-tokens) ----

internal class PpVaultCreateRequest
{
    [JsonPropertyName("payment_source")] public PpVaultPaymentSourceRequest PaymentSource { get; set; } = new();
    [JsonPropertyName("customer")] public PpCustomerRequest? Customer { get; set; }
}

internal class PpVaultPaymentSourceRequest
{
    [JsonPropertyName("card")] public PpCardRequest? Card { get; set; }
}

internal class PpCustomerRequest
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
}

internal class PpVaultTokenResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("customer")] public PpCustomerResponse? Customer { get; set; }
    [JsonPropertyName("payment_source")] public PpVaultPaymentSourceResponse? PaymentSource { get; set; }
    [JsonPropertyName("links")] public List<PpLink>? Links { get; set; }
}

internal class PpCustomerResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
}

internal class PpVaultPaymentSourceResponse
{
    [JsonPropertyName("card")] public PpVaultCardResponse? Card { get; set; }
}

internal class PpVaultCardResponse
{
    [JsonPropertyName("last_digits")] public string? LastDigits { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

// ---- Transaction search (/v1/reporting/transactions) ----

internal class PpTransactionSearchResponse
{
    [JsonPropertyName("transaction_details")] public List<PpTransactionDetail>? TransactionDetails { get; set; }
    [JsonPropertyName("total_items")] public int TotalItems { get; set; }
    [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
}

internal class PpTransactionDetail
{
    [JsonPropertyName("transaction_info")] public PpTransactionInfo? TransactionInfo { get; set; }
}

internal class PpTransactionInfo
{
    [JsonPropertyName("transaction_id")] public string? TransactionId { get; set; }
    [JsonPropertyName("transaction_initiation_date")] public string? TransactionInitiationDate { get; set; }
    [JsonPropertyName("transaction_amount")] public PpMoney? TransactionAmount { get; set; }
    [JsonPropertyName("fee_amount")] public PpMoney? FeeAmount { get; set; }
    [JsonPropertyName("transaction_status")] public string? TransactionStatus { get; set; }
    [JsonPropertyName("custom_field")] public string? CustomField { get; set; }
    [JsonPropertyName("invoice_id")] public string? InvoiceId { get; set; }
}

// ---- OAuth token ----

internal class PpTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}
