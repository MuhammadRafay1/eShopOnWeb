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
using UpvestInvestmentApi.Standard.Models;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Wires up the "invest your change" capability: the Upvest options, the single signed SDK client,
/// the gateway and the application service.
/// </summary>
public static class InvestingServiceRegistration
{
    /// <summary>Scopes this integration needs; they match what the client is entitled to in the sandbox.</summary>
    private static readonly List<OauthScope> RequiredScopes = new()
    {
        OauthScope.Usersadmin, OauthScope.Usersread,
        OauthScope.Checksadmin,
        OauthScope.Accountsadmin, OauthScope.Accountsread,
        OauthScope.Ordersadmin, OauthScope.Ordersread,
        OauthScope.Webhooksadmin,
        OauthScope.Taxesadmin
    };

    public static IServiceCollection AddInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new UpvestOptions();
        configuration.GetSection(UpvestOptions.SectionName).Bind(options);
        services.AddSingleton(Options.Create(options));

        services.AddSingleton(sp => BuildClient(options));

        services.AddScoped<IUpvestInvestingGateway, UpvestGateway>();
        services.AddScoped<IInvestingService, InvestingService>();

        return services;
    }

    private static UpvestInvestmentApiClient BuildClient(UpvestOptions options)
    {
        Require(options.ClientId, nameof(options.ClientId));
        Require(options.ClientSecret, nameof(options.ClientSecret));
        Require(options.BaseUrl, nameof(options.BaseUrl));
        Require(options.SigningKeyId, nameof(options.SigningKeyId));
        Require(options.SigningKeyPath, nameof(options.SigningKeyPath));

        if (!File.Exists(options.SigningKeyPath))
        {
            throw new InvalidOperationException(
                $"Upvest signing key not found at the path configured in {UpvestOptions.SectionName}:SigningKeyPath.");
        }

        var key = UpvestKeys.LoadEcPrivateKey(File.ReadAllText(options.SigningKeyPath), options.SigningKeyPassphrase);

        // The single reusable handler every Upvest call passes through: it signs the request and
        // rewrites it onto the configured base URL. Nothing else attaches credentials.
        var handler = new UpvestSigningHandler(key, options.SigningKeyId, options.ClientId, new Uri(options.BaseUrl));
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };

        return new UpvestInvestmentApiClient.Builder()
            .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(options.ClientId, options.ClientSecret)
                .OauthScopes(RequiredScopes)
                .Build())
            .HttpClientConfig(c => c.HttpClientInstance(httpClient))
            .HttpCallback(new UpvestResponseCapture())
            .Environment(UpvestInvestmentApi.Standard.Environment.Production)
            .Build();
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Upvest configuration '{UpvestOptions.SectionName}:{name}' is missing. " +
                "Supply it via user-secrets or environment configuration.");
        }
    }
}
