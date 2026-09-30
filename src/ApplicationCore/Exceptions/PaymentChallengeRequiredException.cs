using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class PaymentChallengeRequiredException : Exception
{
    public PaymentChallengeRequiredException(string message) : base(message)
    {
    }
}
