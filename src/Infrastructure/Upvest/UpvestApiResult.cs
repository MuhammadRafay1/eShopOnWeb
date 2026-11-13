namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The outcome of a single Upvest API call: either a parsed value, or a non-success status
/// with a non-sensitive error summary extracted from Upvest's <c>problem+json</c> body.
/// </summary>
public class UpvestApiResult<T>
{
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public T? Value { get; init; }

    /// <summary>A short, non-sensitive description of why the call failed (for surfacing/recording).</summary>
    public string? ErrorSummary { get; init; }

    public static UpvestApiResult<T> Success(T value, int statusCode) =>
        new() { IsSuccess = true, StatusCode = statusCode, Value = value };

    public static UpvestApiResult<T> Failure(int statusCode, string? errorSummary) =>
        new() { IsSuccess = false, StatusCode = statusCode, ErrorSummary = errorSummary };
}
