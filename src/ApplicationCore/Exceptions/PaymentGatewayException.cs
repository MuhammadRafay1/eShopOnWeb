using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public enum PaymentGatewayFailureReason
{
    /// <summary>PayPal rejected the request as invalid (bad card, validation error, etc.). Maps to HTTP 422.</summary>
    ProviderRejected,

    /// <summary>PayPal requires a shopper-facing browser approval (3DS/PAYER_ACTION_REQUIRED). Documented STOP path. Maps to HTTP 422.</summary>
    ChallengeRequired,

    /// <summary>The authorization hold is stale and can no longer be renewed (beyond PayPal's reauthorization window). Maps to HTTP 409.</summary>
    ReauthorizationExpired,

    /// <summary>Transient, connection, timeout, or unrecognised provider failure. Maps to HTTP 502.</summary>
    Unavailable
}

/// <summary>
/// The error boundary type for every PayPal call. <see cref="IPayPalPaymentGateway"/> never lets a
/// PayPalServerSdk exception escape; it is always translated into one of these, so ApplicationCore and
/// PublicApi never reference the SDK's exception types.
/// </summary>
public class PaymentGatewayException : Exception
{
    public PaymentGatewayFailureReason Reason { get; }

    /// <summary>PayPal's own correlation id for the failing call (Error.DebugId), when available.</summary>
    public string? ProviderDebugId { get; }

    public PaymentGatewayException(string message, PaymentGatewayFailureReason reason, string? providerDebugId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
        ProviderDebugId = providerDebugId;
    }
}
