using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;
using Models = UpvestInvestmentApi.Standard.Models;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Wires up the "invest your change" capability: the Upvest settings, the one reusable authentication
/// handler, the SDK client, the gateway, the application services and the background reconciliation.
/// </summary>
public static class InvestingDependencies
{
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(options =>
            configuration.GetSection(UpvestSettings.SectionName).Bind(options));

        // Captures the raw body of each Upvest response so the gateway can read ids from responses whose
        // generated models do not match this server's shape.
        services.AddSingleton<UpvestRawResponseCallback>();

        // One SDK client for the lifetime of the app. Its HTTP pipeline is a single reusable handler
        // that authenticates (signs) every request — the OAuth token request included — and pins the
        // configured base URL. No call site attaches credentials.
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;

            var key = UpvestKeys.LoadEcPrivateKey(File.ReadAllText(settings.SigningKeyPath), settings.SigningKeyPassphrase);
            var handler = new UpvestAuthenticationHandler(key, settings.SigningKeyId, settings.ClientId, new Uri(settings.BaseUrl))
            {
                InnerHandler = new HttpClientHandler()
            };
            var httpClient = new HttpClient(handler);

            var scopes = new List<Models.OauthScope>
            {
                Models.OauthScope.Usersadmin,
                Models.OauthScope.Usersread,
                Models.OauthScope.Checksadmin,
                Models.OauthScope.Accountsadmin,
                Models.OauthScope.Accountsread,
                Models.OauthScope.Ordersadmin,
                Models.OauthScope.Ordersread,
                Models.OauthScope.Taxesadmin,
                Models.OauthScope.Webhooksadmin,
                Models.OauthScope.VirtualCashBalancesadmin,
            };

            return new UpvestInvestmentApiClient.Builder()
                .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(settings.ClientId, settings.ClientSecret)
                    .OauthScopes(scopes)
                    .Build())
                .HttpClientConfig(config => config.HttpClientInstance(httpClient, false))
                .HttpCallback(sp.GetRequiredService<UpvestRawResponseCallback>())
                .Build();
        });

        services.AddScoped<IUpvestInvestorGateway, UpvestInvestorGateway>();
        services.AddScoped<IInvestingService, InvestingService>();
        services.AddScoped<IOrderPlacementService, OrderPlacementService>();

        services.AddHostedService<UpvestReconciliationHostedService>();

        return services;
    }
}
