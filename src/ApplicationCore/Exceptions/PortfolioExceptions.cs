using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>A caller request was refused because it was invalid or not permitted by the flow's rules.</summary>
public class PortfolioValidationException : Exception
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>A requested portfolio resource (investor, model, realignment, portfolio) does not exist.</summary>
public class PortfolioNotFoundException : Exception
{
    public PortfolioNotFoundException(string message) : base(message) { }
}
