using System;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Http;

/// <summary>Raised when an Upvest API call returns a non-success status code.</summary>
public sealed class UpvestApiException : Exception
{
    public UpvestApiException(string method, string path, int statusCode, string responseBody)
        : base($"Upvest {method} {path} returned {statusCode}.")
    {
        Method = method;
        Path = path;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string Method { get; }
    public string Path { get; }
    public int StatusCode { get; }
    public string ResponseBody { get; }
}
