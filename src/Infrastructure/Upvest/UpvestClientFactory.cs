using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;
using Models = UpvestInvestmentApi.Standard.Models;
using SdkEnvironment = UpvestInvestmentApi.Standard.Environment;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Builds the single, long-lived <see cref="UpvestInvestmentApiClient"/>. The client is configured
/// with the OAuth client-credentials grant (the SDK fetches and refreshes the token itself) and an
/// <see cref="HttpClient"/> whose only handler is the reusable <see cref="UpvestSigningHandler"/>.
/// Every request — the token request included — is therefore signed and sent to the configured
/// <c>Upvest:BaseUrl</c>.
/// </summary>
public static class UpvestClientFactory
{
    private static readonly List<Models.OauthScope> Scopes = new()
    {
        Models.OauthScope.Usersadmin, Models.OauthScope.Usersread,
        Models.OauthScope.Checksadmin, Models.OauthScope.Checksread,
        Models.OauthScope.Accountsadmin, Models.OauthScope.Accountsread,
        Models.OauthScope.Ordersadmin, Models.OauthScope.Ordersread,
        Models.OauthScope.Positionsread,
        Models.OauthScope.Webhooksadmin, Models.OauthScope.Webhooksread,
        Models.OauthScope.Instrumentsread,
        Models.OauthScope.Taxesadmin, Models.OauthScope.Taxesread,
        Models.OauthScope.VirtualCashBalancesadmin,
    };

    public static UpvestConnection Create(UpvestSettings settings)
    {
        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException(
                "Upvest is not configured. Supply the Upvest:* settings (see UpvestSettings).");
        }

        // The signing handler owns the key for the client's lifetime.
        var key = UpvestKeys.LoadEcPrivateKey(
            File.ReadAllText(settings.SigningKeyPath),
            string.IsNullOrEmpty(settings.SigningKeyPassphrase) ? null : settings.SigningKeyPassphrase);

        // The single, reusable handler that authenticates (signs) every call and routes it to the
        // configured base URL. Used by both the SDK and the direct HTTP client below.
        var signing = new UpvestSigningHandler(key, settings.SigningKeyId, settings.ClientId, new Uri(settings.BaseUrl))
        {
            InnerHandler = new HttpClientHandler()
        };
        var httpClient = new HttpClient(signing) { Timeout = TimeSpan.FromSeconds(100) };

        var client = new UpvestInvestmentApiClient.Builder()
            .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(settings.ClientId, settings.ClientSecret)
                .OauthScopes(Scopes)
                .Build())
            // Route B: our own HttpClient carries the single signing handler. We deliberately do NOT
            // call .HttpSignature(...), which would replace this HttpClient and ignore Upvest:BaseUrl.
            .HttpClientConfig(c => c.HttpClientInstance(httpClient, overrideHttpClientConfiguration: false))
            .Environment(SdkEnvironment.Production)
            .Build();

        return new UpvestConnection(client, httpClient);
    }
}
