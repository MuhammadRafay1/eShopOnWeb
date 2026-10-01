using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Base for payment-flow errors that carry the HTTP status the API should return. The message is always
/// caller-safe (no internal type names, no card data, no raw provider dumps).
/// </summary>
public abstract class PaymentFlowException : Exception
{
    protected PaymentFlowException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    protected PaymentFlowException(string message, int statusCode, Exception inner) : base(message, inner)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

/// <summary>The order does not exist, or does not belong to the caller (404 — existence is not revealed).</summary>
public sealed class OrderNotFoundException : PaymentFlowException
{
    public OrderNotFoundException(int orderId)
        : base($"Order {orderId} was not found.", 404) { }
}

/// <summary>The saved card does not exist, or does not belong to the caller (404).</summary>
public sealed class PaymentMethodNotFoundException : PaymentFlowException
{
    public PaymentMethodNotFoundException(int paymentMethodId)
        : base($"Payment method {paymentMethodId} was not found.", 404) { }
}

/// <summary>The requested action is not valid for the order's current state (409).</summary>
public sealed class InvalidPaymentStateException : PaymentFlowException
{
    public InvalidPaymentStateException(string message) : base(message, 409) { }
}

/// <summary>A refund request was rejected before contacting PayPal (e.g. exceeds the refundable remainder) (400).</summary>
public sealed class RefundValidationException : PaymentFlowException
{
    public RefundValidationException(string message) : base(message, 400) { }
}

/// <summary>A request body was invalid (e.g. empty order, unknown catalog item, no card supplied) (400).</summary>
public sealed class BadPaymentRequestException : PaymentFlowException
{
    public BadPaymentRequestException(string message) : base(message, 400) { }
}

/// <summary>
/// PayPal answered the card payment with a browser-approval challenge. This integration does not build an
/// approval round-trip — per the task, this is a stop-and-report condition (422).
/// </summary>
public sealed class PayerActionRequiredException : PaymentFlowException
{
    public PayerActionRequiredException()
        : base("PayPal requires the shopper to approve this payment in a browser (payer action required). " +
               "This integration does not support an approval round-trip.", 422) { }
}

/// <summary>
/// Internal signal: PayPal reported the authorization as expired when a capture was attempted. The fulfil flow
/// catches this, renews the authorization, and retries the capture once.
/// </summary>
public sealed class AuthorizationExpiredException : PaymentFlowException
{
    public AuthorizationExpiredException(string message, Exception? inner = null)
        : base(message, 409, inner ?? new Exception(message)) { }
}

/// <summary>
/// The authorization had gone stale and could not be renewed before fulfilment. Message is operator-actionable
/// (carries PayPal's own reason) (409).
/// </summary>
public sealed class ReauthorizationFailedException : PaymentFlowException
{
    public ReauthorizationFailedException(string message) : base(message, 409) { }
}

/// <summary>
/// The payment processor returned an error, or could not be reached. Status is 502 for our-fault / transport /
/// unknown provider errors, or the provider's own 4xx when the caller can act on it (e.g. a declined card).
/// </summary>
public sealed class PaymentGatewayException : PaymentFlowException
{
    public PaymentGatewayException(string message, int? providerStatusCode = null, string? providerDebugId = null, Exception? inner = null)
        : base(message, MapStatus(providerStatusCode), inner ?? new Exception(message))
    {
        ProviderStatusCode = providerStatusCode;
        ProviderDebugId = providerDebugId;
    }

    public int? ProviderStatusCode { get; }

    /// <summary>PayPal's own correlation id (debug_id), for support correlation. Never shown to the caller.</summary>
    public string? ProviderDebugId { get; }

    // A provider 400/422/404 is usually the caller's input (declined/invalid card) — surface it so they can act.
    // 401/403/429 are OUR credentials/quota — never a caller fault. Transport/5xx/unknown → 502.
    private static int MapStatus(int? providerStatusCode) => providerStatusCode switch
    {
        400 or 404 or 409 or 422 => providerStatusCode.Value,
        _ => 502
    };
}
