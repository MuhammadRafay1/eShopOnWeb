using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>A caller tried to act as an investor before being accepted, or without a chosen model.</summary>
public class InvestorNotReadyException : Exception
{
    public InvestorNotReadyException(string message) : base(message) { }
}

/// <summary>A model portfolio referenced by a caller does not exist.</summary>
public class ModelPortfolioNotFoundException : Exception
{
    public ModelPortfolioNotFoundException(string modelId)
        : base($"No model portfolio with id '{modelId}' exists.") { }
}

/// <summary>A caller referenced an investor record that does not exist.</summary>
public class InvestorNotFoundException : Exception
{
    public InvestorNotFoundException() : base("The caller is not registered as an investor.") { }
}
