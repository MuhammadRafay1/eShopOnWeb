using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;
using UpvestInvestmentApi.Standard.Models;
using Environment = UpvestInvestmentApi.Standard.Environment;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class InvestingServiceExtensions
{
    /// <summary>
    /// Registers the "invest your change" capability: the Upvest settings, a single long-lived Upvest SDK
    /// client whose every request is authenticated by the one reusable signing handler, the Upvest gateway,
    /// and the investing application service.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new UpvestSettings();
        configuration.GetSection(UpvestSettings.SectionName).Bind(settings);
        services.AddSingleton(settings);

        if (IsConfigured(settings))
        {
            // One signed HttpClient, built once and shared by the SDK client and the raw client. Every request
            // (the token request included) flows through the single UpvestSigningHandler, which points the call
            // at the configured base URL and signs it. No call site attaches credentials.
            var http = BuildSignedHttpClient(settings);
            services.AddSingleton(http);
            services.AddSingleton(_ => BuildClient(settings, http));
            services.AddSingleton(sp => new UpvestRawClient(sp.GetRequiredService<HttpClient>(), sp.GetRequiredService<UpvestInvestmentApiClient>()));
            services.AddScoped<IUpvestGateway, UpvestGateway>();
        }
        else
        {
            // No Upvest configuration (e.g. in tests): the rest of the API runs; investing is disabled.
            services.AddScoped<IUpvestGateway, NotConfiguredUpvestGateway>();
        }

        services.AddScoped<IInvestingService, InvestingService>();

        return services;
    }

    private static bool IsConfigured(UpvestSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.ClientId) &&
        !string.IsNullOrWhiteSpace(settings.ClientSecret) &&
        !string.IsNullOrWhiteSpace(settings.SigningKeyId) &&
        !string.IsNullOrWhiteSpace(settings.SigningKeyPath) &&
        !string.IsNullOrWhiteSpace(settings.BaseUrl) &&
        !string.IsNullOrWhiteSpace(settings.InstrumentId) &&
        !string.IsNullOrWhiteSpace(settings.CallbackBaseUrl) &&
        File.Exists(settings.SigningKeyPath);

    private static HttpClient BuildSignedHttpClient(UpvestSettings settings)
    {
        var key = UpvestKeys.LoadEcPrivateKey(File.ReadAllText(settings.SigningKeyPath), settings.SigningKeyPassphrase);
        var signingHandler = new UpvestSigningHandler(key, settings.SigningKeyId, settings.ClientId, new Uri(settings.BaseUrl), new HttpClientHandler());
        return new HttpClient(signingHandler) { BaseAddress = new Uri(settings.BaseUrl) };
    }

    private static UpvestInvestmentApiClient BuildClient(UpvestSettings settings, HttpClient http)
    {
        var scopes = new List<OauthScope>
        {
            OauthScope.Usersadmin, OauthScope.Usersread,
            OauthScope.Accountsadmin, OauthScope.Accountsread,
            OauthScope.Ordersadmin, OauthScope.Ordersread,
            OauthScope.Instrumentsread,
            OauthScope.Webhooksadmin, OauthScope.Webhooksread,
            OauthScope.VirtualCashBalancesadmin,
            OauthScope.Positionsread,
            OauthScope.Checksadmin, OauthScope.Checksread,
            OauthScope.Taxesadmin, OauthScope.Taxesread
        };

        return new UpvestInvestmentApiClient.Builder()
            .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(settings.ClientId, settings.ClientSecret)
                .OauthScopes(scopes)
                .Build())
            .HttpClientConfig(config => config.HttpClientInstance(http))
            .Environment(Environment.Production)
            .Build();
    }
}
