using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Keeps local enrolment and investment state in step with Upvest. Upvest processes checks,
/// account activation and order settlement asynchronously; this poller (together with the
/// webhook) reconciles pending items so each investment's status reflects what actually
/// happened at Upvest, even if a webhook is never delivered.
/// </summary>
public class UpvestReconciliationHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UpvestReconciliationHostedService> _logger;

    public UpvestReconciliationHostedService(IServiceScopeFactory scopeFactory, ILogger<UpvestReconciliationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureWebhookAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IInvestingService>();
                await service.ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Scheduled reconcile failed: {Message}", ex.Message);
            }
        }
    }

    private async Task EnsureWebhookAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var gateway = scope.ServiceProvider.GetRequiredService<IUpvestInvestingGateway>();
            await gateway.EnsureWebhookSubscriptionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Webhooks are an optimisation on top of polling; carry on if they can't be set up.
            _logger.LogWarning("Could not ensure Upvest webhook subscription: {Message}", ex.Message);
        }
    }
}
