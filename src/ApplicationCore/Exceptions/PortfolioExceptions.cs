using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>A caller supplied something invalid (maps to HTTP 400).</summary>
public class PortfolioValidationException : Exception
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>The caller has not signed up as an investor (maps to HTTP 404).</summary>
public class InvestorNotFoundException : Exception
{
    public InvestorNotFoundException() : base("The caller is not registered as an investor.") { }
}

/// <summary>The caller is an investor but the requested action needs a state they are not in yet
/// (e.g. not accepted/active, or no model chosen) (maps to HTTP 409).</summary>
public class InvestorNotReadyException : Exception
{
    public InvestorNotReadyException(string message) : base(message) { }
}

/// <summary>No model portfolio exists for the given id (maps to HTTP 404).</summary>
public class ModelNotFoundException : Exception
{
    public ModelNotFoundException(string modelId) : base($"No model portfolio found with id '{modelId}'.") { }
}

/// <summary>No realignment exists for the given id (maps to HTTP 404).</summary>
public class RealignmentNotFoundException : Exception
{
    public RealignmentNotFoundException(Guid id) : base($"No realignment found with id '{id}'.") { }
}

/// <summary>
/// A call to Upvest failed. Carries the provider HTTP status (when one was received) so the boundary can
/// decide a caller-facing status: a provider 4xx the caller can act on is surfaced as-is; our own
/// credential/quota failures and transport failures become 5xx. Never carries a provider error body verbatim.
/// </summary>
public class UpvestApiException : Exception
{
    /// <summary>The provider HTTP status, when the provider answered; null for transport/unknown failures.</summary>
    public int? ProviderStatusCode { get; }

    public UpvestApiException(string message, int? providerStatusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderStatusCode = providerStatusCode;
    }
}
