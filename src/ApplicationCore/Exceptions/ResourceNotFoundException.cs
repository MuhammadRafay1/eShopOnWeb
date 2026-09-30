using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A referenced resource does not exist, or does not belong to the caller. Mapped to HTTP 404
/// by the API layer — deliberately not 403, so a shopper cannot probe for the existence of
/// another shopper's resources.
/// </summary>
public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message) : base(message)
    {
    }
}
