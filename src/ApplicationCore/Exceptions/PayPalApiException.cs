using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Represents a structured error returned by any PayPal API. Carries PayPal's own error model
/// (name / message / debug_id / details[].issue — the shape is identical across every spec used)
/// plus the HTTP status PayPal returned, so operators can act on it verbatim rather than seeing a
/// generic 500.
/// </summary>
public class PayPalApiException : Exception
{
    public int HttpStatusCode { get; }
    public string? PayPalName { get; }
    public string? DebugId { get; }
    public IReadOnlyList<PayPalErrorDetail> Details { get; }

    public PayPalApiException(
        int httpStatusCode,
        string? payPalName,
        string? message,
        string? debugId,
        IReadOnlyList<PayPalErrorDetail>? details)
        : base(BuildMessage(payPalName, message, details))
    {
        HttpStatusCode = httpStatusCode;
        PayPalName = payPalName;
        DebugId = debugId;
        Details = details ?? Array.Empty<PayPalErrorDetail>();
    }

    /// <summary>True if any detail issue matches one of the supplied issue codes (case-insensitive).</summary>
    public bool HasIssue(params string[] issues) =>
        Details.Any(d => d.Issue is not null &&
                         issues.Any(i => string.Equals(i, d.Issue, StringComparison.OrdinalIgnoreCase)));

    private static string BuildMessage(string? name, string? message, IReadOnlyList<PayPalErrorDetail>? details)
    {
        var issues = details is { Count: > 0 }
            ? " Issues: " + string.Join("; ", details.Select(d => $"{d.Issue} ({d.Description})"))
            : string.Empty;
        return $"PayPal error: {name}: {message}.{issues}";
    }
}

public class PayPalErrorDetail
{
    public string? Field { get; set; }
    public string? Value { get; set; }
    public string? Issue { get; set; }
    public string? Description { get; set; }
}
