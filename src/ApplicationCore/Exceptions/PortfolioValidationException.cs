using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A caller-supplied value broke a rule the shop enforces before calling Upvest (unknown model, unknown
/// fund ISIN, weights that do not sum to 100, an action requested before the investor is ready).
/// The API boundary maps this to a 4xx the caller can act on.
/// </summary>
public class PortfolioValidationException : Exception
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>
/// The caller tried to do something that requires being an accepted investor (with a model chosen) and is
/// not yet in that state. The API boundary maps this to 409 Conflict.
/// </summary>
public class PortfolioStateException : Exception
{
    public PortfolioStateException(string message) : base(message) { }
}
