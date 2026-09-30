using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The order's payment hold expired and PayPal's reauthorize window (days 4-29 of the original
/// authorization) has also passed, so no further renewal is possible. An operator must ask the
/// shopper to place and pay for a new order.
/// </summary>
public class AuthorizationNotRenewableException : Exception
{
    public AuthorizationNotRenewableException(string message) : base(message)
    {
    }
}
