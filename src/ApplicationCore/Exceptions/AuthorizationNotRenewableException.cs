using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class AuthorizationNotRenewableException : Exception
{
    public AuthorizationNotRenewableException(string message) : base(message)
    {
    }
}
