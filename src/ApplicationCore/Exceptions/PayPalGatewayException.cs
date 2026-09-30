using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Raised when a call to PayPal answers with a non-2xx response. Carries the fields the PayPal
/// error model guarantees (name/message/debug_id) plus any issue codes, for surfacing to an
/// operator. Never carries the original request body (which may contain card data).
/// </summary>
public class PayPalGatewayException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? PayPalName { get; }
    public string? DebugId { get; }
    public IReadOnlyList<string> Issues { get; }

    public PayPalGatewayException(HttpStatusCode statusCode, string? payPalName, string message, string? debugId, IEnumerable<string>? issues = null)
        : base(message)
    {
        StatusCode = statusCode;
        PayPalName = payPalName;
        DebugId = debugId;
        Issues = (issues ?? Enumerable.Empty<string>()).ToList();
    }

    public string ToOperatorMessage()
    {
        var issuesText = Issues.Count > 0 ? string.Join(", ", Issues) : "none";
        return $"PayPal call failed: {Message} [name: {PayPalName ?? "n/a"}, issues: {issuesText}, debug_id: {DebugId ?? "n/a"}]";
    }
}
