using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Catch-all for a PayPal API call that failed in a way this integration does not special-case.
/// Carries PayPal's own error envelope (name / message / debug_id / issue / description) so the
/// failure can be surfaced verbatim to an operator rather than guessed at.
/// </summary>
public class PayPalApiException : Exception
{
    public PayPalApiException(int statusCode, string? name, string? paypalMessage, string? debugId,
        string? issue = null, string? description = null)
        : base(BuildMessage(statusCode, name, paypalMessage, debugId, issue, description))
    {
        StatusCode = statusCode;
        PayPalName = name;
        DebugId = debugId;
        Issue = issue;
        Description = description;
    }

    public int StatusCode { get; }
    public string? PayPalName { get; }
    public string? DebugId { get; }
    public string? Issue { get; }
    public string? Description { get; }

    private static string BuildMessage(int statusCode, string? name, string? message, string? debugId,
        string? issue, string? description)
    {
        var detail = issue is null && description is null ? null : $" [{issue}: {description}]";
        return $"PayPal API call failed (HTTP {statusCode}): {name}: {message}{detail} (debug_id: {debugId}).";
    }
}
