using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Keeps the application's view of enrolments and investments in step with Upvest, and moves matured
/// set-aside balances into the fund — all off the shopper's request path. Runs on a short timer; a
/// webhook from Upvest also triggers a pass. Every pass reads the authoritative state from Upvest, so
/// statuses here always reflect what actually happened there.
/// </summary>
public class UpvestReconciliationHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly IServiceProvider _services;
    private readonly ILogger<UpvestReconciliationHostedService> _logger;

    public UpvestReconciliationHostedService(
        IServiceProvider services,
        ILogger<UpvestReconciliationHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Upvest reconciliation service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var investing = scope.ServiceProvider.GetRequiredService<IInvestingService>();
                await investing.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Upvest reconciliation pass failed: {Error}", ex.Message);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
