namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// PayPal responded that the buyer must approve the payment/card-save in a browser (e.g. a 3DS
/// challenge or PAYER_ACTION_REQUIRED). This integration is direct-card / server-to-server only
/// and does not implement a browser approval round-trip, so this is surfaced as an actionable
/// error rather than silently failing or attempting a redirect.
/// </summary>
public class PayPalBrowserChallengeException : PayPalOperationException
{
    public PayPalBrowserChallengeException(string message)
        : base("PAYER_ACTION_REQUIRED", message, null)
    {
    }
}
