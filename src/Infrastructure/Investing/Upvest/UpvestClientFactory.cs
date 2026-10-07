using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;
using Environment = UpvestInvestmentApi.Standard.Environment;
using Models = UpvestInvestmentApi.Standard.Models;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Upvest;

/// <summary>
/// Builds the single, long-lived <see cref="UpvestInvestmentApiClient"/>. Authentication is wired once
/// here: the OAuth client-credentials grant (the SDK fetches and refreshes the token itself) and the
/// reusable <see cref="UpvestSigningHandler"/> that signs every request — including the token request —
/// and routes it to the configured base URL. No other part of the app configures Upvest credentials.
/// </summary>
public static class UpvestClientFactory
{
    // Scopes this integration needs: manage/read users, KYC checks, accounts, orders; read instruments;
    // fund accounts; manage webhooks.
    private static readonly List<Models.OauthScope> Scopes = new()
    {
        Models.OauthScope.Usersadmin, Models.OauthScope.Usersread,
        Models.OauthScope.Checksadmin, Models.OauthScope.Checksread,
        Models.OauthScope.Accountsadmin, Models.OauthScope.Accountsread,
        Models.OauthScope.Ordersadmin, Models.OauthScope.Ordersread,
        Models.OauthScope.Instrumentsread,
        Models.OauthScope.VirtualCashBalancesadmin,
        Models.OauthScope.Paymentsadmin, Models.OauthScope.Paymentsread,
        Models.OauthScope.Taxesadmin, Models.OauthScope.Taxesread,
        Models.OauthScope.Positionsread,
        Models.OauthScope.Webhooksadmin, Models.OauthScope.Webhooksread,
        Models.OauthScope.Testsadmin
    };

    public static UpvestInvestmentApiClient Create(UpvestOptions options, UpvestResponseCapture responseCapture)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("Upvest:BaseUrl is not configured.");
        }
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException("Upvest:ClientId / Upvest:ClientSecret are not configured.");
        }

        var pem = File.ReadAllText(options.SigningKeyPath);
        var key = UpvestKeys.LoadEcPrivateKey(pem, options.SigningKeyPassphrase);

        // One HttpClient over the single signing handler; the SDK's token request travels the same pipeline.
        var http = new HttpClient(new UpvestSigningHandler(
            key, options.SigningKeyId, options.ClientId, new Uri(options.BaseUrl), new HttpClientHandler()));

        return new UpvestInvestmentApiClient.Builder()
            .Environment(Environment.Production)
            .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(options.ClientId, options.ClientSecret)
                .OauthScopes(Scopes)
                .Build())
            .HttpCallback(responseCapture)
            .HttpClientConfig(config => config
                .HttpClientInstance(http)
                .Timeout(TimeSpan.FromSeconds(30))
                // No automatic retries: mutating calls carry per-request idempotency keys, and the
                // runtime's retry policy would re-send a failed POST with a fresh key, risking a
                // duplicate write. The provider here is local and reliable.
                .NumberOfRetries(0))
            .Build();
    }
}
