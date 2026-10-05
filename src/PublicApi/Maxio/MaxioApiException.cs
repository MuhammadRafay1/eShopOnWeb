using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.Maxio;

/// <summary>
/// Thrown when the Maxio Advanced Billing API returns an error response.
/// Carries the parsed error messages from the spec's error models
/// (Error-List-Response / Customer-Error-Response).
/// </summary>
public class MaxioApiException : Exception
{
    public int StatusCode { get; }

    public IReadOnlyList<string> Errors { get; }

    public MaxioApiException(int statusCode, IReadOnlyList<string> errors)
        : base(ComposeMessage(statusCode, errors))
    {
        StatusCode = statusCode;
        Errors = errors;
    }

    private static string ComposeMessage(int statusCode, IReadOnlyList<string> errors)
    {
        var details = errors.Count > 0 ? string.Join("; ", errors) : "no additional detail provided by the API";
        return $"Maxio Advanced Billing API returned status {statusCode}: {details}";
    }
}