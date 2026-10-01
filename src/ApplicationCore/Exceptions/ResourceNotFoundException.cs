using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A requested resource does not exist, or exists but does not belong to the caller. Deliberately
/// used for both cases (never 403) so an ownership check does not confirm another shopper's resource exists.
/// </summary>
public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string resourceType, object id)
        : base($"{resourceType} '{id}' was not found.")
    {
    }
}
