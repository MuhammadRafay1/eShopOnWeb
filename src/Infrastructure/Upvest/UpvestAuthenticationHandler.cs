using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The single reusable handler that authenticates every call this application makes to Upvest: it attaches
/// the bearer token and HTTP-signs the request. No call site attaches credentials itself. It also captures
/// the raw response so the gateway can read response fields the generated SDK models cannot deserialize.
/// </summary>
public sealed class UpvestAuthenticationHandler : DelegatingHandler
{
    private readonly UpvestTokenProvider _tokenProvider;
    private readonly UpvestRequestSigner _signer;
    private readonly UpvestResponseCapture _capture;

    public UpvestAuthenticationHandler(
        UpvestTokenProvider tokenProvider, UpvestRequestSigner signer, UpvestResponseCapture capture)
    {
        _tokenProvider = tokenProvider;
        _signer = signer;
        _capture = capture;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _tokenProvider.GetTokenAsync(ct).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await _signer.SignAsync(request, ct).ConfigureAwait(false);

        var response = await base.SendAsync(request, ct).ConfigureAwait(false);

        // Buffer the body so both this capture and the SDK's own deserialization can read it.
        await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var requestId = response.Headers.TryGetValues("upvest-request-id", out var ids)
            ? System.Linq.Enumerable.FirstOrDefault(ids)
            : null;
        _capture.Record(response.StatusCode, body, requestId);

        // A 401 means the cached token is stale/invalid; drop it so the next call re-acquires.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            _tokenProvider.Invalidate();

        return response;
    }
}
