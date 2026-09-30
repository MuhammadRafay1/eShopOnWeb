using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A PayPal REST call returned an error response. Carries PayPal's own error "name" (e.g.
/// AUTHORIZATION_EXPIRED) and debug_id so callers can branch on known error names and operators
/// can look the call up in PayPal's dashboard, without ever surfacing card data.
/// </summary>
public class PayPalOperationException : Exception
{
    public string ErrorName { get; }
    public string? DebugId { get; }

    public PayPalOperationException(string errorName, string message, string? debugId)
        : base(message)
    {
        ErrorName = errorName;
        DebugId = debugId;
    }
}
