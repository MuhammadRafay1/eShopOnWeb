namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Raised when Upvest refuses to issue a token for a requested scope ("this client is not authorised
/// to use these scopes"). In the current sandbox only user operations are authorised; the application
/// treats this as a known environment limitation rather than a failure, leaving the affected flow in
/// its last known state.
/// </summary>
public class UpvestScopeNotAuthorizedException : UpvestException
{
    public string Scope { get; }

    public UpvestScopeNotAuthorizedException(string scope)
        : base($"Upvest client is not authorised to use the scope '{scope}' in this environment.")
    {
        Scope = scope;
    }
}
