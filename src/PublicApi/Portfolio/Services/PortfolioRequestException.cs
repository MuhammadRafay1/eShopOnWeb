using System;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

/// <summary>
/// A caller-fixable error in a portfolio request (bad input, not an investor yet, no model chosen, unknown model).
/// Endpoints map <see cref="StatusCode"/> straight onto the HTTP response. Messages are caller-safe and never carry
/// personal data.
/// </summary>
public sealed class PortfolioRequestException : Exception
{
    public PortfolioRequestException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
