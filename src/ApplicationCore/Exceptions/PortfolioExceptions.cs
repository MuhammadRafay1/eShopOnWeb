using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>The caller's request was invalid (maps to HTTP 400).</summary>
public class PortfolioValidationException : Exception
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>A referenced resource does not exist for this caller (maps to HTTP 404).</summary>
public class PortfolioNotFoundException : Exception
{
    public PortfolioNotFoundException(string message) : base(message) { }
}

/// <summary>The request conflicts with current state, e.g. a precondition is not met (maps to HTTP 409).</summary>
public class PortfolioConflictException : Exception
{
    public PortfolioConflictException(string message) : base(message) { }
}
