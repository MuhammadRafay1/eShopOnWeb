using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single, reusable <see cref="DelegatingHandler"/> through which every call this application
/// makes to Upvest passes. It authenticates each call: it attaches the OAuth2 bearer token and
/// signs the request with Upvest HTTP Message Signatures (v15). No other call site attaches
/// credentials. The one exception is the access-token request itself, which carries no bearer
/// (only a signature) — detected here to avoid recursion.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    private readonly IUpvestTokenProvider _tokenProvider;
    private readonly UpvestRequestSigner _signer;
    private readonly UpvestSettings _settings;

    public UpvestAuthenticationHandler(
        IUpvestTokenProvider tokenProvider,
        UpvestRequestSigner signer,
        IOptions<UpvestSettings> settings)
    {
        _tokenProvider = tokenProvider;
        _signer = signer;
        _settings = settings.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isTokenRequest = request.RequestUri is not null &&
            request.RequestUri.AbsolutePath.EndsWith("/auth/token", StringComparison.OrdinalIgnoreCase);

        if (!request.Headers.Contains("accept"))
        {
            request.Headers.TryAddWithoutValidation("accept", "application/json");
        }
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);
        request.Headers.TryAddWithoutValidation("upvest-signature-version", "15");

        if (!isTokenRequest)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (RequiresIdempotencyKey(request.Method))
            {
                request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
            }
        }

        await _signer.SignAsync(request, cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static bool RequiresIdempotencyKey(HttpMethod method) =>
        method == HttpMethod.Post || method == HttpMethod.Put ||
        method == HttpMethod.Patch || method == HttpMethod.Delete;
}
