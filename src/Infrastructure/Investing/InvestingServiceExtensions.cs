using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UpvestInvestmentApi.Standard.Authentication;
using Models = UpvestInvestmentApi.Standard.Models;
using UpvestClient = UpvestInvestmentApi.Standard.UpvestInvestmentApiClient;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Registers the whole "invest your change" capability: the Upvest SDK client
/// (wired to the single signing handler), the gateway, the storage context, the
/// shopper-facing service and the reconciliation worker.
/// </summary>
public static class InvestingServiceExtensions
{
    private static readonly List<Models.OauthScope> Scopes = new()
    {
        Models.OauthScope.Usersadmin,
        Models.OauthScope.Checksadmin,
        Models.OauthScope.Accountsadmin,
        Models.OauthScope.Taxesadmin,
        Models.OauthScope.Webhooksadmin,
        Models.OauthScope.Ordersadmin,
        Models.OauthScope.Ordersread,
        Models.OauthScope.Instrumentsread,
        Models.OauthScope.Positionsread,
        Models.OauthScope.VirtualCashBalancesadmin,
    };

    public static IServiceCollection AddInvestingWithUpvest(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(UpvestOptions.SectionName).Get<UpvestOptions>()
            ?? throw new InvalidOperationException("The 'Upvest' configuration section is missing.");
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));

        // Storage, isolated from the catalog so the feature is additive. Honour the
        // same in-memory switch the rest of the app uses on this machine.
        var useOnlyInMemory = bool.TryParse(configuration["UseOnlyInMemoryDatabase"], out var v) && v;
        services.AddDbContext<InvestingContext>(options =>
        {
            if (useOnlyInMemory)
                options.UseInMemoryDatabase("Investing");
            else
                options.UseSqlServer(configuration.GetConnectionString("CatalogConnection"));
        });

        // One SDK client for the whole app, built once (it is immutable and
        // long-lived) with its own HttpClient whose only handler is the reusable
        // signing handler. The OAuth token request flows through the same handler.
        services.AddSingleton(sp =>
        {
            var key = UpvestKeys.LoadEcPrivateKey(File.ReadAllText(options.SigningKeyPath), options.SigningKeyPassphrase);
            var handler = new UpvestSigningHandler(key, options.SigningKeyId, options.ClientId, new Uri(options.BaseUrl))
            {
                InnerHandler = new HttpClientHandler()
            };
            var http = new HttpClient(handler);

            return new UpvestClient.Builder()
                .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(options.ClientId, options.ClientSecret)
                    .OauthScopes(Scopes)
                    .Build())
                .HttpClientConfig(c => c.HttpClientInstance(http))
                .Build();
        });

        services.AddSingleton<IUpvestGateway, UpvestGateway>();
        services.AddScoped<IInvestingService, InvestingService>();
        services.AddScoped<IOrderPlacementService, OrderPlacementService>();
        services.AddHostedService<InvestingReconciliationWorker>();

        return services;
    }
}
