using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class OrderWithNoItemsException : Exception
{
    public OrderWithNoItemsException()
        : base("An order must contain at least one item.")
    {
    }

    public OrderWithNoItemsException(string message) : base(message)
    {
    }

    public OrderWithNoItemsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
