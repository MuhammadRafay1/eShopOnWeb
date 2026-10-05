using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Periodic sweep that drives the asynchronous flows: it advances pending enrolments, invests
/// balances that have reached the threshold, and reconciles in-flight investments with Upvest.
/// This is the safety net that guarantees state converges even if a webhook is never delivered
/// (which matters here, where everything runs in-memory within a single process run).
/// </summary>
public sealed class InvestingBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvestingBackgroundService> _logger;

    public InvestingBackgroundService(IServiceScopeFactory scopeFactory, ILogger<InvestingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IInvestingProcessor>();

                await processor.ReconcilePendingEnrolmentsAsync(stoppingToken);
                await processor.ExecuteReadyInvestmentsAsync(stoppingToken);
                await processor.ReconcilePendingInvestmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Investing background sweep failed; will retry next tick.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
