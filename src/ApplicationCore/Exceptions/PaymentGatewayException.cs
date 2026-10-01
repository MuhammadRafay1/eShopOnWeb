using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The application's own payment-gateway failure type. Every PayPal SDK exception is translated into
/// this (or a subclass) inside the Infrastructure gateway, so no SDK type ever crosses out of
/// Infrastructure and the rest of the app has one failure type to reason about. Carries the HTTP
/// status (when the provider answered) and PayPal's correlation id (<see cref="DebugId"/>) so an
/// operator can hand it to PayPal support.
/// </summary>
public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, HttpStatusCode? statusCode = null, string? debugId = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        DebugId = debugId;
    }

    public HttpStatusCode? StatusCode { get; }
    public string? DebugId { get; }
}

/// <summary>
/// The provider was unreachable, or a write's connection failed after the request may have been
/// acted on. The outcome is <b>unknown</b>, not failed — callers must settle it (re-read / park as
/// Unknown), never report a plain failure or let the shopper be charged silently.
/// </summary>
public class PaymentOutcomeUnknownException : PaymentGatewayException
{
    public PaymentOutcomeUnknownException(string message, Exception? inner = null)
        : base(message, statusCode: null, debugId: null, inner: inner)
    {
    }
}

/// <summary>
/// PayPal answered a card payment (or a card vaulting request) with a payer-action / 3DS challenge
/// that would require the shopper to approve in a browser. Per the task this is a STOP condition: we
/// surface it rather than building a browser approval round-trip.
/// </summary>
public class PayPalPayerActionRequiredException : PaymentGatewayException
{
    public PayPalPayerActionRequiredException(string message)
        : base(message)
    {
    }
}
