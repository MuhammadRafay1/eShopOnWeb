using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// The single reusable <see cref="DelegatingHandler"/> through which every outbound Upvest call is
/// authenticated. It attaches the OAuth bearer token (on every call except the token endpoint itself)
/// and the HTTP Message Signature headers (<c>signature</c> / <c>signature-input</c>). No call site
/// attaches any credential: call sites leave these as placeholders and this handler overwrites them.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    private readonly IUpvestTokenProvider _tokenProvider;
    private readonly UpvestRequestSigner _signer;

    public UpvestAuthenticationHandler(IUpvestTokenProvider tokenProvider, UpvestRequestSigner signer)
    {
        _tokenProvider = tokenProvider;
        _signer = signer;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri
            ?? throw new InvalidOperationException("Upvest request has no URI.");

        var isTokenEndpoint = uri.AbsolutePath.EndsWith("/auth/token", StringComparison.OrdinalIgnoreCase);

        if (!isTokenEndpoint)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var headers = _signer.Sign(request.Method.Method, uri, DateTimeOffset.UtcNow);

        request.Headers.Remove("signature");
        request.Headers.Remove("signature-input");
        request.Headers.TryAddWithoutValidation("signature", headers.Signature);
        request.Headers.TryAddWithoutValidation("signature-input", headers.SignatureInput);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
