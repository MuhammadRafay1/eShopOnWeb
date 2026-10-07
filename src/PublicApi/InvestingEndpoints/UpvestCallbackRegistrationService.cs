using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Investing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// At startup, best-effort registers this host's Upvest webhook callback so the provider can
/// push order/execution updates to <c>api/investing/upvest-webhook</c>. Does not block start,
/// and never fails the app — settlement is also reconciled when the balance/investments
/// endpoints are read.
/// </summary>
public sealed class UpvestCallbackRegistrationService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UpvestSettings _settings;

    public UpvestCallbackRegistrationService(IServiceScopeFactory scopeFactory, IOptions<UpvestSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.CallbackBaseUrl))
        {
            return Task.CompletedTask;
        }

        var callbackUrl = _settings.CallbackBaseUrl.TrimEnd('/') + "/api/investing/upvest-webhook";

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var provider = scope.ServiceProvider.GetRequiredService<IInvestmentProvider>();
                await provider.RegisterCallbackAsync(callbackUrl, cancellationToken);
            }
            catch
            {
                // RegisterCallbackAsync is already best-effort; ignore anything that escapes.
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
