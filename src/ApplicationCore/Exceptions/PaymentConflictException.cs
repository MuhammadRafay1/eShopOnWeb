using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The requested payment action does not make sense given the order's current payment state
/// (e.g. capturing before authorization, cancelling after capture, refunding beyond what was
/// captured).
/// </summary>
public class PaymentConflictException : Exception
{
    public PaymentConflictException(string message) : base(message)
    {
    }
}
