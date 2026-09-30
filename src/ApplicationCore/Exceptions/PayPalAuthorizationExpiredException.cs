using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Internal signal thrown by the gateway when a capture fails because the authorization has
/// expired (PayPal issue <c>AUTHORIZATION_EXPIRED</c>). The payment service catches this to
/// reauthorize once and retry the capture, rather than failing fulfilment outright.
/// </summary>
public class PayPalAuthorizationExpiredException : Exception
{
    public PayPalAuthorizationExpiredException(string? issue, string? description)
        : base($"Authorization expired ({issue}: {description}).")
    {
        Issue = issue;
        Description = description;
    }

    public string? Issue { get; }
    public string? Description { get; }
}
