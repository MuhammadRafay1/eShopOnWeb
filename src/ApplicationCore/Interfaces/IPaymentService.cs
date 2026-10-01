using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the money-movement flows end to end: placing an order, authorizing a hold, capturing at
/// fulfilment, cancelling (void), refunding, saved cards, and reconciliation. Each action stays separately
/// invocable. Ownership is enforced here (shopper-scoped methods take a buyer id and filter by it);
/// operator-scoped methods assume the caller is already restricted to the administrator role.
/// </summary>
public interface IPaymentService
{
    Task<int> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLine> lines, ShipTo shipTo, CancellationToken ct);

    Task<OrderPaymentView> PayAsync(string buyerId, int orderId, PayCommand command, CancellationToken ct);

    Task<OrderPaymentView> FulfilAsync(int orderId, CancellationToken ct);

    Task<OrderPaymentView> CancelAsync(int orderId, CancellationToken ct);

    Task<RefundResultView> RefundAsync(int orderId, decimal? amount, string idempotencyKey, CancellationToken ct);

    Task<IReadOnlyList<OrderPaymentView>> GetMyOrdersAsync(string buyerId, CancellationToken ct);

    Task<ReconciliationView> ReconcileAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

    Task<SavedCardView> SaveCardAsync(string buyerId, CardDetails card, CancellationToken ct);

    Task<IReadOnlyList<SavedCardView>> GetSavedCardsAsync(string buyerId, CancellationToken ct);

    Task DeleteSavedCardAsync(string buyerId, int paymentMethodId, CancellationToken ct);
}

public sealed record OrderLine(int CatalogItemId, int Quantity);

public sealed record ShipTo(string Street, string City, string State, string Country, string ZipCode);

public sealed record PayCommand
{
    /// <summary>Raw one-off card details, or null when paying with a saved card.</summary>
    public CardDetails? Card { get; init; }

    /// <summary>The caller's saved-card id (the app's own int id), or null for a one-off card.</summary>
    public int? PaymentMethodId { get; init; }
}

public sealed record OrderPaymentView
{
    public required int OrderId { get; init; }
    public required string Status { get; init; }
    public required decimal Total { get; init; }
    public required string Currency { get; init; }
    public string? PayPalOrderId { get; init; }
    public string? AuthorizationId { get; init; }
    public string? AuthorizationStatus { get; init; }
    public decimal? AuthorizedAmount { get; init; }
    public DateTimeOffset? AuthorizationExpiresAt { get; init; }
    public string? CaptureId { get; init; }
    public string? CaptureStatus { get; init; }
    public decimal? CapturedAmount { get; init; }
    public decimal? PayPalFee { get; init; }
    public decimal? NetProceeds { get; init; }
    public decimal RefundedAmount { get; init; }
    public IReadOnlyList<RefundView> Refunds { get; init; } = Array.Empty<RefundView>();
}

public sealed record RefundView
{
    public required int RefundId { get; init; }
    public string? PayPalRefundId { get; init; }
    public required decimal Amount { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record RefundResultView
{
    public required int RefundId { get; init; }
    public required OrderPaymentView Order { get; init; }
}

public sealed record SavedCardView
{
    public required int PaymentMethodId { get; init; }
    public string? Brand { get; init; }
    public string? LastDigits { get; init; }
    public string? Expiry { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record ReconciliationView
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }

    /// <summary>True when PayPal's own records were cut short by a hard page cap (report is partial).</summary>
    public required bool Truncated { get; init; }

    /// <summary>Every transaction PayPal reports for the range, each lined up to an eShop order (or not).</summary>
    public required IReadOnlyList<ReconciliationEntry> PayPalTransactions { get; init; }

    /// <summary>eShop orders captured in the range that PayPal's records do not (yet) show.</summary>
    public required IReadOnlyList<int> EShopOrdersMissingFromPayPal { get; init; }
}

public sealed record ReconciliationEntry
{
    public string? TransactionId { get; init; }
    public string? ReferenceId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? Status { get; init; }
    public string? InvoiceId { get; init; }
    public DateTimeOffset? InitiatedAt { get; init; }

    /// <summary>The eShop order this PayPal transaction lines up with, or null if eShop does not know it.</summary>
    public int? MatchedOrderId { get; init; }
}
