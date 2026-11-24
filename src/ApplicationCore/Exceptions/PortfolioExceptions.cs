using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Thrown when a shopper has not yet signed up as an investor.</summary>
public class InvestorNotFoundException : Exception
{
    public InvestorNotFoundException() : base("No investor sign-up was found for the caller.") { }
}

/// <summary>Thrown when a shopper tries to pay in before Upvest has accepted them with a model chosen.</summary>
public class InvestorNotReadyException : Exception
{
    public InvestorNotReadyException(string message) : base(message) { }
}

/// <summary>Thrown when the caller is already signed up as an investor.</summary>
public class DuplicateInvestorException : Exception
{
    public DuplicateInvestorException() : base("The caller is already registered as an investor.") { }
}

/// <summary>Thrown when a referenced model portfolio does not exist.</summary>
public class ModelPortfolioNotFoundException : Exception
{
    public ModelPortfolioNotFoundException(string modelId)
        : base($"No model portfolio with id '{modelId}' exists.") { }
}
