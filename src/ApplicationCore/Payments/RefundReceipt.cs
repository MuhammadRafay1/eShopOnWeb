namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>
/// The outcome of a refund: the local refund id (the caller's <c>refundId</c>), its status and
/// amount, plus how much of the capture can still be refunded and the order's resulting status.
/// </summary>
public record RefundReceipt(
    int RefundId,
    string Status,
    decimal Amount,
    string Currency,
    decimal RemainingRefundable,
    string OrderStatus);
