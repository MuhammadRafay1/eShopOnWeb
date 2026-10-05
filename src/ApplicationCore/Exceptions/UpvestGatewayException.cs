using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A single failure type the investing domain handles for every provider interaction. The Infrastructure
/// gateway converts the provider's SDK exceptions (typed API errors, transport faults, deserialization,
/// auth) into this so callers reason about one kind of failure.
/// </summary>
public class UpvestGatewayException : Exception
{
    public UpvestGatewayException(string message, Exception? inner = null, int? statusCode = null, bool outcomeUnknown = false)
        : base(message, inner)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
    }

    /// <summary>The provider HTTP status, when the provider answered.</summary>
    public int? StatusCode { get; }

    /// <summary>
    /// True when a write may have reached the provider before the connection failed — the outcome is
    /// unknown and must be settled by re-reading provider state, not reported as a failure.
    /// </summary>
    public bool OutcomeUnknown { get; }
}
