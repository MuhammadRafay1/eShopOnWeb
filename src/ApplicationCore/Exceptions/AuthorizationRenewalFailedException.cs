using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The order's PayPal authorization went stale before fulfilment and could not be renewed
/// (e.g. past PayPal's ~29-day reauthorization window). The order stays Authorized; an operator
/// must act (typically: have the shopper pay again to create a fresh authorization).
/// </summary>
public class AuthorizationRenewalFailedException : Exception
{
    public AuthorizationRenewalFailedException(string message) : base(message)
    {
    }
}
