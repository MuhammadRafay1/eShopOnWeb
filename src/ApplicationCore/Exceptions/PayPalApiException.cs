using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal returns a non-2xx response. Carries the parsed standard error
/// envelope (name / debug_id / details[]) plus the HTTP status so PublicApi endpoints
/// can map it to an appropriate client-facing status and log the debug_id.
/// Never carries request/response bodies (which can contain card data).
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
        string message,
        string? debugId,
        IReadOnlyList<PayPalErrorDetail>? details)
        : base(message)
    {
        HttpStatusCode = httpStatusCode;
        PayPalName = payPalName;
        DebugId = debugId;
        Details = details ?? Array.Empty<PayPalErrorDetail>();
    }

    /// <summary>True for 4xx business/validation rejections (as opposed to transport/5xx).</summary>
    public bool IsClientError => HttpStatusCode >= 400 && HttpStatusCode < 500;

    public string DescribeIssues()
    {
        if (Details.Count == 0)
        {
            return PayPalName ?? Message;
        }
        return string.Join("; ", System.Linq.Enumerable.Select(Details,
            d => string.IsNullOrEmpty(d.Field) ? $"{d.Issue}: {d.Description}" : $"{d.Issue} ({d.Field}): {d.Description}"));
    }
}

public class PayPalErrorDetail
{
    public string? Issue { get; init; }
    public string? Field { get; init; }
    public string? Value { get; init; }
    public string? Description { get; init; }
}
