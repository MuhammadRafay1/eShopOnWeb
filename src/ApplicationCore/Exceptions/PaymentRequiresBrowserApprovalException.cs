namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// PayPal responded to a card payment with a challenge that requires the shopper to approve it in a
/// browser. This integration is built for direct, non-interactive card processing only; this exception
/// surfaces that gap rather than attempting an approval round-trip.
/// </summary>
public class PaymentRequiresBrowserApprovalException : PaymentGatewayException
{
    public PaymentRequiresBrowserApprovalException(string payPalOrderId)
        : base($"PayPal order {payPalOrderId} requires payer approval in a browser; this integration " +
               "only supports direct, non-interactive card payments.")
    {
    }
}
