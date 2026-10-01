using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>An order was not found, or is not owned by the caller. Surfaced as 404 to avoid leaking existence.</summary>
public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int orderId) : base($"No order found with id {orderId}.")
    {
    }
}

/// <summary>A saved card was not found, or is not owned by the caller. Surfaced as 404.</summary>
public class PaymentMethodNotFoundException : Exception
{
    public PaymentMethodNotFoundException(int paymentMethodId) : base($"No saved payment method found with id {paymentMethodId}.")
    {
    }
}

/// <summary>A refund would exceed what remains refundable on the capture. Surfaced as 400.</summary>
public class OverRefundException : Exception
{
    public OverRefundException(decimal requested, decimal remaining)
        : base($"Requested refund {requested:0.00} exceeds the refundable remaining {remaining:0.00}.")
    {
    }
}
