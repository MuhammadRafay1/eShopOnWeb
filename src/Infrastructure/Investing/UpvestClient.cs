using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;
using UpvestInvestmentApi.Standard.Models;
using SdkEnvironment = UpvestInvestmentApi.Standard.Environment;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Builds and owns the single, long-lived Upvest SDK client. The client is
/// configured once with OAuth 2 client-credentials and wired to a shared
/// <see cref="HttpClient"/> whose pipeline ends in the one
/// <see cref="UpvestSigningHandler"/> — so every call (the OAuth token request
/// included) is signed and routed to the configured base URL.
/// </summary>
public sealed class UpvestClient : IDisposable
{
    // The SDK requires a GUID for the per-request "upvest-client-id" header, but this
    // application's client id is not a GUID. The signing handler overwrites that header
    // with the real client id, so the value passed through the SDK is immaterial.
    public static readonly Guid HeaderClientId = Guid.Empty;

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUrl;

    public UpvestInvestmentApiClient Api { get; }

    public UpvestClient(IOptions<UpvestSettings> options)
    {
        var settings = options.Value;
        _baseUrl = new Uri(settings.BaseUrl);

        var pem = File.ReadAllText(settings.SigningKeyPath);
        var key = UpvestKeyLoader.LoadEcPrivateKey(pem, settings.SigningKeyPassphrase);

        var signingHandler = new UpvestSigningHandler(
            key,
            settings.SigningKeyId,
            settings.ClientId,
            new Uri(settings.BaseUrl),
            new HttpClientHandler());

        _httpClient = new HttpClient(signingHandler);

        Api = new UpvestInvestmentApiClient.Builder()
            .Environment(SdkEnvironment.Production)
            .ClientCredentialsAuth(
                new ClientCredentialsAuthModel.Builder(settings.ClientId, settings.ClientSecret)
                    .OauthScopes(new List<OauthScope>
                    {
                        OauthScope.Usersadmin,
                        OauthScope.Usersread,
                        OauthScope.Accountsadmin,
                        OauthScope.Accountsread,
                        OauthScope.Ordersadmin,
                        OauthScope.Ordersread,
                        OauthScope.Webhooksadmin,
                        OauthScope.Webhooksread,
                        OauthScope.Taxesadmin,
                        OauthScope.Taxesread,
                        OauthScope.Checksadmin,
                        OauthScope.Checksread,
                        OauthScope.Instrumentsread,
                        OauthScope.VirtualCashBalancesadmin,
                    })
                    .Build())
            // Route the SDK (and its OAuth token request) through our signing pipeline.
            // Do NOT also call .HttpSignature(...): that would replace this HttpClient.
            .HttpClientConfig(c => c.HttpClientInstance(_httpClient))
            .Build();
    }

    /// <summary>
    /// Issues a GET through the same signed pipeline and OAuth token the SDK uses, and
    /// returns the parsed JSON body. This exists only to read the few responses whose
    /// generated SDK models reject the live payload (they mark fields <c>[JsonRequired]</c>
    /// that Upvest does not always return, which makes the typed call throw). Returns
    /// <c>null</c> for an empty or non-JSON body.
    /// </summary>
    public async System.Threading.Tasks.Task<Newtonsoft.Json.Linq.JObject?> GetJsonAsync(string path)
    {
        var token = await Api.ClientCredentialsAuth.FetchTokenAsync();
        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUrl, path));
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token.AccessToken}");
        using var resp = await _httpClient.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(body) ? null : Newtonsoft.Json.Linq.JObject.Parse(body);
    }

    public void Dispose() => _httpClient.Dispose();
}
