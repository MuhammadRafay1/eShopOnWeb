using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A referenced resource (e.g. a saved card id named in a pay request) does not exist or is not
/// owned by the caller. Deliberately indistinguishable from "does not exist" so ownership is
/// never leaked to a non-owner.
/// </summary>
public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message) : base(message)
    {
    }
}
