using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Base type for expected, caller-facing managed-portfolio errors.</summary>
public abstract class PortfolioException : Exception
{
    protected PortfolioException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The caller is not an investor (no investor record for their identity).</summary>
public sealed class InvestorNotFoundException : PortfolioException
{
    public InvestorNotFoundException()
        : base("The caller is not registered as an investor.") { }
}

/// <summary>
/// The caller cannot pay in or choose a model because they are not an accepted investor with
/// an account ready, or because no model has been chosen.
/// </summary>
public sealed class NotAnAcceptedInvestorException : PortfolioException
{
    public NotAnAcceptedInvestorException(string message) : base(message) { }
}

/// <summary>A referenced model portfolio does not exist.</summary>
public sealed class ModelPortfolioNotFoundException : PortfolioException
{
    public ModelPortfolioNotFoundException(string modelId)
        : base($"Model portfolio '{modelId}' does not exist.") { }
}

/// <summary>Caller input failed a shop-side business rule (e.g. model weights do not total 100).</summary>
public sealed class PortfolioValidationException : PortfolioException
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>
/// An interaction with Upvest failed. Carries the provider HTTP status (when one was received)
/// so the boundary can tell a caller-fault (4xx) from a provider/transport fault. The message is
/// caller-safe and never carries personal data or provider internals.
/// </summary>
public sealed class UpvestProviderException : PortfolioException
{
    public UpvestProviderException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
