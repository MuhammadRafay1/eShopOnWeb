using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A structured error returned by PayPal, mapped from the standard error schema
/// (name, message, debug_id, details[].issue) defined in the specs. Carrying the parsed fields
/// lets callers branch on the error rather than parsing an opaque 500.
/// </summary>
public class PayPalApiException : Exception
{
    public int StatusCode { get; }
    public string? Name { get; }
    public string? DebugId { get; }
    public IReadOnlyList<string> Issues { get; }

    public PayPalApiException(int statusCode, string? name, string message, string? debugId,
        IEnumerable<string>? issues)
        : base(BuildMessage(name, message, issues))
    {
        StatusCode = statusCode;
        Name = name;
        DebugId = debugId;
        Issues = issues?.ToList() ?? new List<string>();
    }

    private static string BuildMessage(string? name, string message, IEnumerable<string>? issues)
    {
        var issueText = issues is null ? null : string.Join("; ", issues);
        return string.IsNullOrEmpty(issueText)
            ? $"PayPal error {name}: {message}"
            : $"PayPal error {name}: {message} ({issueText})";
    }

    /// <summary>True if any of PayPal's issue codes matches (case-insensitive).</summary>
    public bool HasIssue(params string[] codes) =>
        Issues.Any(i => codes.Any(c => string.Equals(i, c, StringComparison.OrdinalIgnoreCase)))
        || (Name is not null && codes.Any(c => string.Equals(Name, c, StringComparison.OrdinalIgnoreCase)));
}
