using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class RefundExceedsCaptureException : Exception
{
    public RefundExceedsCaptureException(string message) : base(message)
    {
    }
}
