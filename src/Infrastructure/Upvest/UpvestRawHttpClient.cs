using System;
using System.Net.Http;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// A signed <see cref="HttpClient"/> (same <see cref="UpvestAuthenticationHandler"/> pipeline as
/// the SDK client) for the few raw calls the generated SDK cannot round-trip. The vendored SDK's
/// account-group models expect a <c>users</c> array, but this API returns a <c>user_id</c> field,
/// so every typed account-group response fails to deserialize; we read those responses as raw JSON
/// instead. The capability itself is exposed by the SDK — this only works around a model mismatch.
/// </summary>
public sealed class UpvestRawHttpClient
{
    public UpvestRawHttpClient(HttpClient http, Uri baseUrl)
    {
        Http = http;
        BaseUrl = baseUrl;
    }

    public HttpClient Http { get; }
    public Uri BaseUrl { get; }
}
