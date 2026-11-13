using System;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>Base for portfolio domain errors that map to a specific HTTP status at the API boundary.</summary>
public abstract class PortfolioException : Exception
{
    protected PortfolioException(string message) : base(message) { }
}

/// <summary>The caller's request is invalid (maps to 400).</summary>
public sealed class PortfolioValidationException : PortfolioException
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>A referenced resource does not exist for this caller (maps to 404).</summary>
public sealed class PortfolioNotFoundException : PortfolioException
{
    public PortfolioNotFoundException(string message) : base(message) { }
}

/// <summary>
/// The caller is not in a state that allows the action — e.g. paying in before Upvest has accepted
/// them as an investor with a model chosen (maps to 409).
/// </summary>
public sealed class PortfolioConflictException : PortfolioException
{
    public PortfolioConflictException(string message) : base(message) { }
}
