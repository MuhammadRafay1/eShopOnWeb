using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class CatalogItemNotFoundException : Exception
{
    public CatalogItemNotFoundException(int catalogItemId)
        : base($"No catalog item found with id {catalogItemId}.")
    {
    }

    public CatalogItemNotFoundException(string message) : base(message)
    {
    }

    public CatalogItemNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
