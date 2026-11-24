using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Adds the bearer token, an Accept header, and (for write methods) an Idempotency-Key to
/// each Upvest API request. Runs before the signing handler so these headers are covered by
/// the signature.
/// </summary>
public sealed class UpvestAuthHandler : DelegatingHandler
{
    private readonly UpvestTokenProvider _tokenProvider;

    public UpvestAuthHandler(UpvestTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (request.Headers.Accept.Count == 0)
            request.Headers.Accept.ParseAdd("application/json");

        var method = request.Method.Method;
        var isWrite = method is "POST" or "PUT" or "PATCH" or "DELETE";
        if (isWrite && !request.Headers.Contains("idempotency-key"))
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
