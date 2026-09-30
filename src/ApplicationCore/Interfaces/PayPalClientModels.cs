using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

// Plain input/result contracts for IPayPalClient. These live in ApplicationCore (not
// Infrastructure) so the interface has no dependency on the wire-format DTOs used internally
// by the PayPal HTTP client. None of these ever carry the card number or CVV.

public class PayPalCardInput
{
    public string? Name { get; init; }
    public required string Number { get; init; }
    public required string Expiry { get; init; }
    public required string SecurityCode { get; init; }
    public PayPalBillingAddressInput? BillingAddress { get; init; }
}

public class PayPalBillingAddressInput
{
    public string? AddressLine1 { get; init; }
    public string? AdminArea1 { get; init; }
    public string? AdminArea2 { get; init; }
    public string? PostalCode { get; init; }
    public required string CountryCode { get; init; }
}

/// <summary>
/// Either a raw card (one-off payment, optionally vaulted on success) or a previously saved
/// vault id - exactly one of Card / VaultId is set.
/// </summary>
public class PayPalCreateOrderInput
{
    public required int OrderId { get; init; }
    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }
    public PayPalCardInput? Card { get; init; }
    public string? VaultId { get; init; }
    public bool SaveCardOnSuccess { get; init; }
    public string? ExistingPayPalCustomerId { get; init; }
    public string? MerchantCustomerId { get; init; }
}

public class PayPalOrderResult
{
    public required string PayPalOrderId { get; init; }
    public required string Status { get; init; }
}

/// <summary>Result of authorizing a PayPal order (the hold).</summary>
public class PayPalAuthorizeResult
{
    public required string PayPalOrderId { get; init; }
    public required string OrderStatus { get; init; }
    public string? AuthorizationId { get; init; }
    public string? AuthorizationStatus { get; init; }
    public DateTimeOffset? AuthorizationExpiresAt { get; init; }

    /// <summary>True when PayPal requires a browser-based payer approval to proceed.</summary>
    public bool RequiresPayerAction { get; init; }

    public string? VaultId { get; init; }
    public string? VaultCustomerId { get; init; }
    public string? CardBrand { get; init; }
    public string? CardLastDigits { get; init; }
    public string? CardExpiry { get; init; }
}

/// <summary>Result of a reauthorize (renew) call.</summary>
public class PayPalAuthorizationStatusResult
{
    public required string AuthorizationId { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public class PayPalCaptureResult
{
    public required string CaptureId { get; init; }
    public required string Status { get; init; }
    public required decimal GrossAmount { get; init; }
    public decimal? FeeAmount { get; init; }
    public decimal? NetAmount { get; init; }
}

public class PayPalRefundResult
{
    public required string RefundId { get; init; }
    public required string Status { get; init; }
    public required decimal Amount { get; init; }
}

public class PayPalVaultTokenResult
{
    public required string VaultId { get; init; }
    public required string PayPalCustomerId { get; init; }
    public string? Brand { get; init; }
    public string? LastDigits { get; init; }
    public string? Expiry { get; init; }
}

public class PayPalTransaction
{
    public required string TransactionId { get; init; }
    public string? Status { get; init; }
    public string? InvoiceId { get; init; }
    public string? CustomField { get; init; }
    public DateTimeOffset? InitiationDate { get; init; }
    public decimal? Amount { get; init; }
    public string? CurrencyCode { get; init; }
    public decimal? FeeAmount { get; init; }
}
