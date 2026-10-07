using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UpvestInvestmentApi.Standard;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Sends requests to Upvest over the very same signed <see cref="HttpClient"/> the SDK uses (so every call
/// still flows through the one reusable signing handler), adding the OAuth bearer token obtained through the
/// SDK's auth manager, and parses the JSON responses leniently.
///
/// It exists only to sidestep a defect in the generated SDK: several response models (Account, AccountGroup
/// and the order models) mark fields such as <c>users</c> as required, while the Upvest API does not always
/// return them, so the SDK's strict deserializer throws on otherwise-successful responses. The endpoints and
/// payload shapes are exactly those the SDK documents.
/// </summary>
public sealed class UpvestRawClient
{
    private readonly HttpClient _http;
    private readonly UpvestInvestmentApiClient _client;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token;
    private long _expiryUnixSeconds;

    public UpvestRawClient(HttpClient http, UpvestInvestmentApiClient client)
    {
        _http = http;
        _client = client;
    }

    public Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, path, null, cancellationToken);

    public Task<JsonElement> PostAsync(string path, object body, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, path, body, cancellationToken);

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken));
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        // The signing handler adds upvest-client-id / upvest-api-version / idempotency-key and signs the request
        // (the Authorization header set above is covered by the signature, exactly as the SDK does it).
        using var response = await _http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new UpvestGatewayException(path, (int)response.StatusCode, new Exception("Upvest returned a non-success status."));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return default;
        }

        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_token is not null && _expiryUnixSeconds > now + 30)
        {
            return _token;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_token is not null && _expiryUnixSeconds > now + 30)
            {
                return _token;
            }

            var token = await _client.ClientCredentialsAuth.FetchTokenAsync();
            _token = token.AccessToken;
            _expiryUnixSeconds = token.Expiry ?? now + 3600;
            return _token!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
