namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown by the PayPal gateway when a capture attempt fails because the authorization has passed its
/// honor period / is otherwise stale. The caller (payment service) should attempt a reauthorization and
/// retry the capture once before giving up.
/// </summary>
public class PayPalAuthorizationStaleException : PayPalException
{
    public string AuthorizationId { get; }

    public PayPalAuthorizationStaleException(string authorizationId, string message, string? issueCode = null)
        : base(message, 409, issueCode)
    {
        AuthorizationId = authorizationId;
    }
}
