using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal.Models;

// Wire DTOs mapping the documented PayPal JSON request/response shapes (Orders v2, Payments v2,
// Vault v3, Transaction Search v1). snake_case via [JsonPropertyName]. Nulls are omitted on
// serialization (see PayPalClient's JsonSerializerOptions) so optional fields stay absent.

// ---- Shared ----
public class PayPalMoney
{
    [JsonPropertyName("currency_code")] public string CurrencyCode { get; set; } = "";
    [JsonPropertyName("value")] public string Value { get; set; } = "";
}

public class PayPalLink
{
    [JsonPropertyName("href")] public string? Href { get; set; }
    [JsonPropertyName("rel")] public string? Rel { get; set; }
    [JsonPropertyName("method")] public string? Method { get; set; }
}

// ---- OAuth ----
public class PayPalTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")] public string? TokenType { get; set; }
}

// ---- Error envelope ----
public class PayPalErrorResponse
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("debug_id")] public string? DebugId { get; set; }
    [JsonPropertyName("details")] public List<PayPalErrorDetail>? Details { get; set; }
}

public class PayPalErrorDetail
{
    [JsonPropertyName("issue")] public string? Issue { get; set; }
    [JsonPropertyName("field")] public string? Field { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

// ---- Card / billing address ----
public class PayPalCardBillingAddress
{
    [JsonPropertyName("address_line_1")] public string? AddressLine1 { get; set; }
    [JsonPropertyName("address_line_2")] public string? AddressLine2 { get; set; }
    [JsonPropertyName("admin_area_2")] public string? AdminArea2 { get; set; }
    [JsonPropertyName("admin_area_1")] public string? AdminArea1 { get; set; }
    [JsonPropertyName("postal_code")] public string? PostalCode { get; set; }
    [JsonPropertyName("country_code")] public string? CountryCode { get; set; }
}

public class PayPalCardRequest
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("number")] public string? Number { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
    [JsonPropertyName("security_code")] public string? SecurityCode { get; set; }
    [JsonPropertyName("billing_address")] public PayPalCardBillingAddress? BillingAddress { get; set; }
    [JsonPropertyName("vault_id")] public string? VaultId { get; set; }
}

// ---- Create Order (Orders v2) ----
public class PayPalCreateOrderRequest
{
    [JsonPropertyName("intent")] public string Intent { get; set; } = "AUTHORIZE";
    [JsonPropertyName("purchase_units")] public List<PayPalPurchaseUnitRequest> PurchaseUnits { get; set; } = new();
    [JsonPropertyName("payment_source")] public PayPalPaymentSourceRequest? PaymentSource { get; set; }
}

public class PayPalPurchaseUnitRequest
{
    [JsonPropertyName("custom_id")] public string? CustomId { get; set; }
    [JsonPropertyName("amount")] public PayPalMoney Amount { get; set; } = new();
}

public class PayPalPaymentSourceRequest
{
    [JsonPropertyName("card")] public PayPalCardRequest? Card { get; set; }
}

// ---- Order response ----
public class PayPalOrderResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("purchase_units")] public List<PayPalPurchaseUnitResponse>? PurchaseUnits { get; set; }
    [JsonPropertyName("links")] public List<PayPalLink>? Links { get; set; }
}

public class PayPalPurchaseUnitResponse
{
    [JsonPropertyName("payments")] public PayPalPayments? Payments { get; set; }
}

public class PayPalPayments
{
    [JsonPropertyName("authorizations")] public List<PayPalAuthorization>? Authorizations { get; set; }
    [JsonPropertyName("captures")] public List<PayPalCapture>? Captures { get; set; }
}

public class PayPalAuthorization
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("expiration_time")] public System.DateTimeOffset? ExpirationTime { get; set; }
    [JsonPropertyName("status_details")] public PayPalStatusDetails? StatusDetails { get; set; }
    [JsonPropertyName("amount")] public PayPalMoney? Amount { get; set; }
}

public class PayPalStatusDetails
{
    [JsonPropertyName("reason")] public string? Reason { get; set; }
}

// ---- Capture ----
public class PayPalCaptureRequest
{
    [JsonPropertyName("final_capture")] public bool FinalCapture { get; set; } = true;
}

public class PayPalCapture
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("amount")] public PayPalMoney? Amount { get; set; }
    [JsonPropertyName("seller_receivable_breakdown")] public PayPalSellerReceivableBreakdown? SellerReceivableBreakdown { get; set; }
}

public class PayPalSellerReceivableBreakdown
{
    [JsonPropertyName("gross_amount")] public PayPalMoney? GrossAmount { get; set; }
    [JsonPropertyName("paypal_fee")] public PayPalMoney? PayPalFee { get; set; }
    [JsonPropertyName("net_amount")] public PayPalMoney? NetAmount { get; set; }
}

// ---- Reauthorize ----
public class PayPalReauthorizeRequest
{
    [JsonPropertyName("amount")] public PayPalMoney Amount { get; set; } = new();
}

// ---- Refund ----
public class PayPalRefundRequest
{
    [JsonPropertyName("amount")] public PayPalMoney? Amount { get; set; }
}

public class PayPalRefundResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("amount")] public PayPalMoney? Amount { get; set; }
}

// ---- Vault v3 ----
public class PayPalSetupTokenRequest
{
    [JsonPropertyName("payment_source")] public PayPalSetupTokenPaymentSource PaymentSource { get; set; } = new();
}

public class PayPalSetupTokenPaymentSource
{
    [JsonPropertyName("card")] public PayPalVaultCardRequest? Card { get; set; }
}

public class PayPalVaultCardRequest
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("number")] public string? Number { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
    [JsonPropertyName("security_code")] public string? SecurityCode { get; set; }
    [JsonPropertyName("billing_address")] public PayPalCardBillingAddress? BillingAddress { get; set; }
    [JsonPropertyName("verification_method")] public string? VerificationMethod { get; set; }
    [JsonPropertyName("experience_context")] public PayPalVaultExperienceContext? ExperienceContext { get; set; }
}

public class PayPalVaultExperienceContext
{
    // Only used by PayPal if a contingency (3DS/challenge) is triggered — which this server-side
    // integration STOPs on. Required by the vault API regardless.
    [JsonPropertyName("return_url")] public string? ReturnUrl { get; set; }
    [JsonPropertyName("cancel_url")] public string? CancelUrl { get; set; }
}

public class PayPalSetupTokenResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("links")] public List<PayPalLink>? Links { get; set; }
}

public class PayPalPaymentTokenRequest
{
    [JsonPropertyName("payment_source")] public PayPalPaymentTokenSource PaymentSource { get; set; } = new();
}

public class PayPalPaymentTokenSource
{
    [JsonPropertyName("token")] public PayPalTokenReference Token { get; set; } = new();
}

public class PayPalTokenReference
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "SETUP_TOKEN";
}

public class PayPalPaymentTokenResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("payment_source")] public PayPalPaymentTokenResponseSource? PaymentSource { get; set; }
}

public class PayPalPaymentTokenResponseSource
{
    [JsonPropertyName("card")] public PayPalCardResponse? Card { get; set; }
}

public class PayPalCardResponse
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("last_digits")] public string? LastDigits { get; set; }
    [JsonPropertyName("expiry")] public string? Expiry { get; set; }
}

// ---- Transaction Search v1 ----
public class PayPalTransactionSearchResponse
{
    [JsonPropertyName("transaction_details")] public List<PayPalTransactionDetail>? TransactionDetails { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("total_items")] public int TotalItems { get; set; }
    [JsonPropertyName("total_pages")] public int TotalPages { get; set; }
}

public class PayPalTransactionDetail
{
    [JsonPropertyName("transaction_info")] public PayPalTransactionInfo? TransactionInfo { get; set; }
}

public class PayPalTransactionInfo
{
    [JsonPropertyName("transaction_id")] public string? TransactionId { get; set; }
    [JsonPropertyName("transaction_event_code")] public string? TransactionEventCode { get; set; }
    [JsonPropertyName("transaction_initiation_date")] public System.DateTimeOffset? TransactionInitiationDate { get; set; }
    [JsonPropertyName("transaction_amount")] public PayPalMoney? TransactionAmount { get; set; }
    [JsonPropertyName("transaction_status")] public string? TransactionStatus { get; set; }
    [JsonPropertyName("custom_field")] public string? CustomField { get; set; }
}
