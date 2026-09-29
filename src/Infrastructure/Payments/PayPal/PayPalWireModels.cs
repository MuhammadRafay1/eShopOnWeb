using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.Infrastructure.Payments.PayPal;

// Internal wire DTOs mirroring PayPal's JSON exactly. Property names are PascalCase and mapped to
// PayPal's snake_case via JsonNamingPolicy.SnakeCaseLower (see PayPalApiClient.JsonOptions).

// ---- Orders v2: create ----

internal class CreateOrderWire
{
    public string Intent { get; set; } = "AUTHORIZE";
    public List<PurchaseUnitWire> PurchaseUnits { get; set; } = new();
    public PaymentSourceWire? PaymentSource { get; set; }
}

internal class PurchaseUnitWire
{
    public string? InvoiceId { get; set; }
    public AmountWire Amount { get; set; } = new();
}

internal class AmountWire
{
    public string CurrencyCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

internal class PaymentSourceWire
{
    public CardWire? Card { get; set; }
}

internal class CardWire
{
    public string? Name { get; set; }
    public string? Number { get; set; }
    public string? Expiry { get; set; }
    public string? SecurityCode { get; set; }
    public BillingAddressWire? BillingAddress { get; set; }
    public string? VaultId { get; set; }
    public StoredCredentialWire? StoredCredential { get; set; }
}

internal class BillingAddressWire
{
    public string? AddressLine1 { get; set; }
    public string? AdminArea2 { get; set; } // city
    public string? AdminArea1 { get; set; } // state / province
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}

internal class StoredCredentialWire
{
    public string PaymentInitiator { get; set; } = "CUSTOMER";
    public string PaymentType { get; set; } = "UNSCHEDULED";
    public string Usage { get; set; } = "SUBSEQUENT";
}

// ---- Orders v2 / Payments v2: responses ----

internal class OrderResponseWire
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public List<PurchaseUnitResponseWire>? PurchaseUnits { get; set; }
}

internal class PurchaseUnitResponseWire
{
    public PaymentsWire? Payments { get; set; }
}

internal class PaymentsWire
{
    public List<AuthorizationWire>? Authorizations { get; set; }
}

internal class AuthorizationWire
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? ExpirationTime { get; set; }
    public StatusDetailsWire? StatusDetails { get; set; }
}

internal class StatusDetailsWire
{
    public string? Reason { get; set; }
}

internal class CaptureResponseWire
{
    public string? Id { get; set; }
    public string? Status { get; set; }
    public SellerReceivableBreakdownWire? SellerReceivableBreakdown { get; set; }
}

internal class SellerReceivableBreakdownWire
{
    public MoneyWire? GrossAmount { get; set; }
    public MoneyWire? PaypalFee { get; set; }
    public MoneyWire? NetAmount { get; set; }
}

internal class MoneyWire
{
    public string? CurrencyCode { get; set; }
    public string? Value { get; set; }
}

internal class RefundResponseWire
{
    public string? Id { get; set; }
    public string? Status { get; set; }
}

// ---- Vault v3 ----

internal class VaultTokenRequestWire
{
    public CustomerWire Customer { get; set; } = new();
    public PaymentSourceWire PaymentSource { get; set; } = new();
}

internal class CustomerWire
{
    public string Id { get; set; } = string.Empty;
}

internal class VaultTokenResponseWire
{
    public string? Id { get; set; }
    public CustomerWire? Customer { get; set; }
    public VaultPaymentSourceResponseWire? PaymentSource { get; set; }
}

internal class VaultPaymentSourceResponseWire
{
    public CardResponseWire? Card { get; set; }
}

internal class CardResponseWire
{
    public string? LastDigits { get; set; }
    public string? Brand { get; set; }
    public string? Expiry { get; set; }
    public string? Name { get; set; }
}

// ---- Reporting v1 ----

internal class TransactionSearchResponseWire
{
    public int? TotalItems { get; set; }
    public int? TotalPages { get; set; }
    public List<TransactionDetailWire>? TransactionDetails { get; set; }
}

internal class TransactionDetailWire
{
    public TransactionInfoWire? TransactionInfo { get; set; }
}

internal class TransactionInfoWire
{
    public string? TransactionId { get; set; }
    public MoneyWire? TransactionAmount { get; set; }
    public string? TransactionStatus { get; set; }
    public DateTimeOffset? TransactionInitiationDate { get; set; }
    public string? InvoiceId { get; set; }
    public MoneyWire? FeeAmount { get; set; }
}
