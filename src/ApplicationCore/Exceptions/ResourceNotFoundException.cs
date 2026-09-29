using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The requested resource does not exist, or exists but is not owned by the caller. Ownership
/// failures deliberately return 404 (not 403) so a caller can't confirm another shopper's
/// resource exists. Maps to 404 Not Found.
/// </summary>
public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message) : base(message)
    {
    }
}
