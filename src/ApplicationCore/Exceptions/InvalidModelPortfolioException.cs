using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a model portfolio definition is invalid (e.g. weights do not add up to 100).
/// </summary>
public class InvalidModelPortfolioException : Exception
{
    public InvalidModelPortfolioException(string message) : base(message)
    {
    }
}
