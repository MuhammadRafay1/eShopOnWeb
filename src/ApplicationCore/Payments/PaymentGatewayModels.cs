using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>Raw card details for a one-off payment or for saving a card. Never persisted, never logged.</summary>
public sealed record CardDetails(
    string Number,
    string Expiry,
    string? SecurityCode,
    string? CardholderName,
    CardBillingAddress? BillingAddress)
{
    public string LastDigits => Number.Length >= 4 ? Number[^4..] : Number;

    // Records print every member by default; make sure card data can never reach a log through ToString().
    public override string ToString() => $"Card ending {LastDigits}";
}

public sealed record CardBillingAddress(
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string CountryCode);

/// <summary>What pays for an authorization: either a card entered now, or a card the shopper saved earlier.</summary>
public abstract record PaymentInstrument;

public sealed record CardInstrument(CardDetails Card) : PaymentInstrument
{
    public override string ToString() => Card.ToString();
}

public sealed record VaultedCardInstrument(string VaultTokenId) : PaymentInstrument;

public sealed record CreateAuthorizationOrderCommand(
    int EshopOrderId,
    decimal Amount,
    string Currency,
    PaymentInstrument Instrument,
    string ProviderRequestId);

public enum ProviderOrderState { Created, Saved, Approved, Voided, Completed, PayerActionRequired, Other }

public enum ProviderAuthorizationState { Created, Pending, Captured, PartiallyCaptured, Denied, Voided, Other }

public enum ProviderCaptureState { Completed, Pending, PartiallyRefunded, Refunded, Declined, Failed, Other }

public enum ProviderRefundState { Completed, Pending, Failed, Cancelled, Other }

public sealed record ProviderAuthorization(
    string Id,
    ProviderAuthorizationState State,
    string? RawStatus,
    string? StatusReason,
    decimal? Amount,
    string? Currency,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record ProviderCapture(
    string Id,
    ProviderCaptureState State,
    string? RawStatus,
    decimal? Amount,
    string? Currency,
    decimal? Fee,
    decimal? Net,
    DateTimeOffset? CreatedAt);

public sealed record ProviderRefund(
    string Id,
    ProviderRefundState State,
    string? RawStatus,
    decimal? Amount,
    string? Currency,
    string? CustomId,
    DateTimeOffset? CreatedAt);

public sealed record ProviderOrder(
    string Id,
    ProviderOrderState State,
    string? RawStatus,
    IReadOnlyList<ProviderAuthorization> Authorizations,
    IReadOnlyList<ProviderCapture> Captures,
    IReadOnlyList<ProviderRefund> Refunds,
    string? CardBrand,
    string? CardLastDigits);

public sealed record ProviderVaultedCard(
    string VaultTokenId,
    string? CustomerId,
    string? Brand,
    string? LastDigits,
    string? Expiry);

public sealed record ProviderTransaction(
    string TransactionId,
    string? EventCode,
    string? Status,
    decimal? Amount,
    string? Currency,
    decimal? Fee,
    DateTimeOffset? InitiatedAt,
    string? CustomField,
    string? InvoiceId,
    string? ReferenceId);

public sealed record ProviderTransactionPage(
    IReadOnlyList<ProviderTransaction> Transactions,
    int Page,
    int TotalPages,
    int? TotalItems);
