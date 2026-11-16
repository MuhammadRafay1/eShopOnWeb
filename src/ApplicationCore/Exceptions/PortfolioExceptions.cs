using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>The caller has not signed up as an investor.</summary>
public class InvestorNotFoundException : Exception
{
    public InvestorNotFoundException(string message) : base(message) { }
}

/// <summary>A referenced model portfolio does not exist.</summary>
public class ModelNotFoundException : Exception
{
    public ModelNotFoundException(string message) : base(message) { }
}

/// <summary>A pay-in was refused because the caller is not an accepted investor with a model chosen.</summary>
public class PaymentRefusedException : Exception
{
    public PaymentRefusedException(string message) : base(message) { }
}
