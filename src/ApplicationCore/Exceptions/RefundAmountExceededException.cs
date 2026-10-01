namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The requested refund would push the order's cumulative refunded total beyond what was captured.
/// </summary>
public class RefundAmountExceededException : PaymentException
{
    public RefundAmountExceededException(int orderId, decimal requested, decimal remaining)
        : base($"Order {orderId}: refund of {requested:0.00} exceeds the {remaining:0.00} still available to refund.")
    {
    }
}
