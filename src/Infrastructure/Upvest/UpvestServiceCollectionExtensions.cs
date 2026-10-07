using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;
using UpvestInvestmentApi.Standard.Models;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Registers everything needed to invest a shopper's spare change via Upvest: the settings,
/// the single signing <see cref="UpvestAuthenticationHandler"/>, the (singleton) SDK client,
/// the gateway, and the application service.
/// </summary>
public static class UpvestServiceCollectionExtensions
{
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(options => configuration.GetSection(UpvestSettings.SectionName).Bind(options));

        // One SDK client for the whole app, built lazily so an unconfigured Upvest section
        // never stops the rest of PublicApi from starting — only the investing endpoints fail.
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            return BuildClient(settings);
        });

        // A separately-built signed HttpClient for the handful of raw calls the SDK cannot
        // deserialize (see UpvestRawHttpClient). Same signing pipeline, its own key instance.
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            Require(settings.BaseUrl, nameof(settings.BaseUrl));
            Require(settings.SigningKeyPath, nameof(settings.SigningKeyPath));
            var key = UpvestKeys.LoadEcPrivateKey(File.ReadAllText(settings.SigningKeyPath), settings.SigningKeyPassphrase);
            var handler = new UpvestAuthenticationHandler(key, settings.SigningKeyId, settings.ClientId, new Uri(settings.BaseUrl))
            {
                InnerHandler = new HttpClientHandler()
            };
            return new UpvestRawHttpClient(new HttpClient(handler), new Uri(settings.BaseUrl));
        });

        services.AddScoped<IUpvestInvestingGateway, UpvestInvestingGateway>();
        services.AddScoped<IInvestingService, InvestingService>();
        return services;
    }

    private static UpvestInvestmentApiClient BuildClient(UpvestSettings settings)
    {
        Require(settings.BaseUrl, nameof(settings.BaseUrl));
        Require(settings.ClientId, nameof(settings.ClientId));
        Require(settings.ClientSecret, nameof(settings.ClientSecret));
        Require(settings.SigningKeyId, nameof(settings.SigningKeyId));
        Require(settings.SigningKeyPath, nameof(settings.SigningKeyPath));
        Require(settings.InstrumentId, nameof(settings.InstrumentId));

        if (!File.Exists(settings.SigningKeyPath))
        {
            throw new FileNotFoundException("Upvest signing key not found.", settings.SigningKeyPath);
        }

        ECDsa key = UpvestKeys.LoadEcPrivateKey(File.ReadAllText(settings.SigningKeyPath), settings.SigningKeyPassphrase);

        // The one reusable handler every Upvest call (the token request included) passes through.
        var handler = new UpvestAuthenticationHandler(key, settings.SigningKeyId, settings.ClientId, new Uri(settings.BaseUrl))
        {
            InnerHandler = new HttpClientHandler()
        };
        var httpClient = new HttpClient(handler);

        // Exactly the scopes this integration uses. (The sandbox/mock rejects the whole token
        // if it is asked for a scope the tenant is not granted, e.g. prices:read.)
        var scopes = new List<OauthScope>
        {
            OauthScope.Usersadmin,
            OauthScope.Checksadmin,
            OauthScope.Taxesadmin,
            OauthScope.Accountsadmin, OauthScope.Accountsread,
            OauthScope.Ordersadmin, OauthScope.Ordersread,
            OauthScope.Webhooksadmin, OauthScope.Webhooksread,
            OauthScope.VirtualCashBalancesadmin, OauthScope.Paymentsread
        };

        // Route B: the SDK's client-credentials manager owns the OAuth token; our handler
        // (wired through HttpClientConfig) signs every request, including the token request.
        // Do NOT also call .HttpSignature(...) — it would replace our HttpClient.
        return new UpvestInvestmentApiClient.Builder()
            .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(settings.ClientId, settings.ClientSecret)
                .OauthScopes(scopes)
                .Build())
            .HttpClientConfig(c => c.HttpClientInstance(httpClient))
            .Build();
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Upvest configuration '{UpvestSettings.SectionName}:{name}' is required but was not provided.");
        }
    }
}
