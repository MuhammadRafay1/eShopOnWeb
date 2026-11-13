using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>The caller's request is invalid (e.g. fund weights do not sum to 100). Maps to 400.</summary>
public class PortfolioValidationException : Exception
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>
/// The caller is not in a state that permits the action (e.g. paying in before Upvest has accepted
/// them as an investor, or before a model has been chosen). Maps to 409.
/// </summary>
public class InvestorStateException : Exception
{
    public InvestorStateException(string message) : base(message) { }
}

/// <summary>A referenced portfolio resource (model, realignment, …) does not exist. Maps to 404.</summary>
public class PortfolioNotFoundException : Exception
{
    public PortfolioNotFoundException(string message) : base(message) { }
}
