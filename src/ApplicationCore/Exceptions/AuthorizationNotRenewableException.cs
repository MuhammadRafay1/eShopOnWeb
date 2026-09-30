using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown at fulfilment when a stale authorization could not be renewed (reauthorized) — for
/// example it is now too old for PayPal to reauthorize at all. Carries PayPal's raw issue and
/// description verbatim so the operator is told, in actionable terms, that the authorization
/// cannot be renewed and the order needs to be cancelled and the shopper asked to pay again.
/// </summary>
public class AuthorizationNotRenewableException : Exception
{
    public AuthorizationNotRenewableException(string? issue, string? description)
        : base(BuildMessage(issue, description))
    {
        Issue = issue;
        Description = description;
    }

    public string? Issue { get; }
    public string? Description { get; }

    private static string BuildMessage(string? issue, string? description)
        => "The payment authorization has expired and could not be renewed" +
           (issue is null && description is null
               ? "."
               : $" (PayPal: {issue} - {description}).") +
           " Cancel the order to release the hold and ask the shopper to pay again.";
}
