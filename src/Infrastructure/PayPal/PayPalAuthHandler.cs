using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Attaches a bearer token to every outgoing PayPal business-API call, retrying once on 401
/// after invalidating the cached token (in case it expired early or was revoked).
/// </summary>
public class PayPalAuthHandler : DelegatingHandler
{
    private readonly IPayPalTokenProvider _tokenProvider;

    public PayPalAuthHandler(IPayPalTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _tokenProvider.InvalidateCache();
            response.Dispose();

            var retryToken = await _tokenProvider.GetAccessTokenAsync(ct);
            using var retryRequest = await CloneAsync(request);
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", retryToken);
            return await base.SendAsync(retryRequest, ct);
        }

        return response;
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        foreach (var header in original.Headers)
        {
            if (header.Key == "Authorization") continue;
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }
}
