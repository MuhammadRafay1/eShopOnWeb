using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

// Wire-shape DTOs for the PayPal REST APIs (Orders v2, Payments v2, Vault v3, Transaction Search v1).
// Kept internal to the Infrastructure layer -- IPayPalClient returns small application-facing DTOs
// instead, so nothing outside this file/PayPalClient.cs depends on PayPal's JSON shape.

internal record PayPalTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

internal record PayPalMoney(
    [property: JsonPropertyName("currency_code")] string CurrencyCode,
    [property: JsonPropertyName("value")] string Value);

internal record PayPalAddressRequest(
    [property: JsonPropertyName("address_line_1")] string? AddressLine1,
    [property: JsonPropertyName("address_line_2")] string? AddressLine2,
    [property: JsonPropertyName("admin_area_2")] string? AdminArea2,
    [property: JsonPropertyName("admin_area_1")] string? AdminArea1,
    [property: JsonPropertyName("postal_code")] string? PostalCode,
    [property: JsonPropertyName("country_code")] string CountryCode);

internal record PayPalCardRequest(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("expiry")] string? Expiry,
    [property: JsonPropertyName("security_code")] string? SecurityCode,
    [property: JsonPropertyName("billing_address")] PayPalAddressRequest? BillingAddress,
    [property: JsonPropertyName("vault_id")] string? VaultId);

internal record PayPalPaymentSourceRequest(
    [property: JsonPropertyName("card")] PayPalCardRequest Card);

internal record PayPalPurchaseUnitRequest(
    [property: JsonPropertyName("amount")] PayPalMoney Amount,
    [property: JsonPropertyName("custom_id")] string CustomId,
    [property: JsonPropertyName("invoice_id")] string InvoiceId);

internal record PayPalCreateOrderRequest(
    [property: JsonPropertyName("intent")] string Intent,
    [property: JsonPropertyName("purchase_units")] List<PayPalPurchaseUnitRequest> PurchaseUnits,
    [property: JsonPropertyName("payment_source")] PayPalPaymentSourceRequest PaymentSource);

internal record PayPalLink(
    [property: JsonPropertyName("rel")] string? Rel,
    [property: JsonPropertyName("href")] string? Href);

internal record PayPalCardResponse(
    [property: JsonPropertyName("brand")] string? Brand,
    [property: JsonPropertyName("last_digits")] string? LastDigits,
    [property: JsonPropertyName("expiry")] string? Expiry,
    [property: JsonPropertyName("authentication_result")] PayPalAuthenticationResult? AuthenticationResult);

internal record PayPalAuthenticationResult(
    [property: JsonPropertyName("three_d_secure")] PayPalThreeDSecure? ThreeDSecure);

internal record PayPalThreeDSecure(
    [property: JsonPropertyName("authentication_status")] string? AuthenticationStatus,
    [property: JsonPropertyName("enrollment_status")] string? EnrollmentStatus);

internal record PayPalPaymentSourceResponse(
    [property: JsonPropertyName("card")] PayPalCardResponse? Card);

internal record PayPalAuthorization(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expiration_time")] System.DateTimeOffset? ExpirationTime);

internal record PayPalPayments(
    [property: JsonPropertyName("authorizations")] List<PayPalAuthorization>? Authorizations);

internal record PayPalPurchaseUnitResponse(
    [property: JsonPropertyName("payments")] PayPalPayments? Payments);

internal record PayPalOrderResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("payment_source")] PayPalPaymentSourceResponse? PaymentSource,
    [property: JsonPropertyName("purchase_units")] List<PayPalPurchaseUnitResponse>? PurchaseUnits,
    [property: JsonPropertyName("links")] List<PayPalLink>? Links);

internal record PayPalCaptureRequest(
    [property: JsonPropertyName("amount")] PayPalMoney Amount,
    [property: JsonPropertyName("final_capture")] bool FinalCapture);

internal record PayPalSellerReceivableBreakdown(
    [property: JsonPropertyName("gross_amount")] PayPalMoney? GrossAmount,
    [property: JsonPropertyName("paypal_fee")] PayPalMoney? PayPalFee,
    [property: JsonPropertyName("net_amount")] PayPalMoney? NetAmount);

internal record PayPalCaptureResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("amount")] PayPalMoney? Amount,
    [property: JsonPropertyName("seller_receivable_breakdown")] PayPalSellerReceivableBreakdown? SellerReceivableBreakdown);

internal record PayPalReauthorizeRequest(
    [property: JsonPropertyName("amount")] PayPalMoney Amount);

internal record PayPalReauthorizeResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expiration_time")] System.DateTimeOffset? ExpirationTime);

internal record PayPalAuthorizationDetailsResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expiration_time")] System.DateTimeOffset? ExpirationTime);

internal record PayPalRefundRequest(
    [property: JsonPropertyName("amount")] PayPalMoney? Amount);

internal record PayPalSellerPayableBreakdown(
    [property: JsonPropertyName("total_refunded_amount")] PayPalMoney? TotalRefundedAmount);

internal record PayPalRefundResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("amount")] PayPalMoney? Amount,
    [property: JsonPropertyName("seller_payable_breakdown")] PayPalSellerPayableBreakdown? SellerPayableBreakdown);

internal record PayPalVaultCustomerRequest(
    [property: JsonPropertyName("id")] string? Id);

internal record PayPalVaultCreateRequest(
    [property: JsonPropertyName("payment_source")] PayPalPaymentSourceRequest PaymentSource,
    [property: JsonPropertyName("customer")] PayPalVaultCustomerRequest? Customer);

internal record PayPalVaultCustomerResponse(
    [property: JsonPropertyName("id")] string? Id);

internal record PayPalVaultCardResponse(
    [property: JsonPropertyName("brand")] string? Brand,
    [property: JsonPropertyName("last_digits")] string? LastDigits,
    [property: JsonPropertyName("expiry")] string? Expiry);

internal record PayPalVaultPaymentSourceResponse(
    [property: JsonPropertyName("card")] PayPalVaultCardResponse? Card);

internal record PayPalVaultCreateResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("customer")] PayPalVaultCustomerResponse? Customer,
    [property: JsonPropertyName("payment_source")] PayPalVaultPaymentSourceResponse? PaymentSource);

internal record PayPalTransactionAmount(
    [property: JsonPropertyName("currency_code")] string? CurrencyCode,
    [property: JsonPropertyName("value")] string? Value);

internal record PayPalTransactionInfo(
    [property: JsonPropertyName("transaction_id")] string? TransactionId,
    [property: JsonPropertyName("transaction_event_code")] string? TransactionEventCode,
    [property: JsonPropertyName("transaction_status")] string? TransactionStatus,
    [property: JsonPropertyName("transaction_amount")] PayPalTransactionAmount? TransactionAmount,
    [property: JsonPropertyName("fee_amount")] PayPalTransactionAmount? FeeAmount,
    [property: JsonPropertyName("invoice_id")] string? InvoiceId,
    [property: JsonPropertyName("custom_field")] string? CustomField,
    [property: JsonPropertyName("transaction_initiation_date")] System.DateTimeOffset? TransactionInitiationDate);

internal record PayPalTransactionDetail(
    [property: JsonPropertyName("transaction_info")] PayPalTransactionInfo? TransactionInfo);

internal record PayPalTransactionSearchResponse(
    [property: JsonPropertyName("transaction_details")] List<PayPalTransactionDetail>? TransactionDetails,
    [property: JsonPropertyName("total_pages")] int? TotalPages);

internal record PayPalErrorDetail(
    [property: JsonPropertyName("issue")] string? Issue,
    [property: JsonPropertyName("field")] string? Field,
    [property: JsonPropertyName("description")] string? Description);

internal record PayPalErrorResponse(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("debug_id")] string? DebugId,
    [property: JsonPropertyName("details")] List<PayPalErrorDetail>? Details);
