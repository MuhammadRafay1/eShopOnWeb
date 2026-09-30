using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class RefundAmountExceedsRemainingException : Exception
{
    public RefundAmountExceedsRemainingException(string message) : base(message)
    {
    }
}
