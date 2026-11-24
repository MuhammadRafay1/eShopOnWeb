using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Invalid input for a managed-portfolio operation (maps to HTTP 400).</summary>
public class PortfolioValidationException : Exception
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>
/// The requested operation conflicts with the caller's current state, e.g. paying in before
/// becoming an accepted investor with a model chosen (maps to HTTP 409).
/// </summary>
public class PortfolioConflictException : Exception
{
    public PortfolioConflictException(string message) : base(message) { }
}

/// <summary>A requested managed-portfolio resource does not exist for the caller (maps to HTTP 404).</summary>
public class PortfolioNotFoundException : Exception
{
    public PortfolioNotFoundException(string message) : base(message) { }
}
