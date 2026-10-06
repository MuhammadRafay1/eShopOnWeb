using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Hosted service that drives <see cref="IInvestmentReconciler"/> on a timer so investment and enrolment
/// statuses converge to what actually happened at Upvest, even without any webhook delivery.
/// </summary>
public sealed class SettlementReconciler : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SettlementReconciler> _logger;

    public SettlementReconciler(IServiceScopeFactory scopeFactory, ILogger<SettlementReconciler> logger)
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
                var reconciler = scope.ServiceProvider.GetRequiredService<IInvestmentReconciler>();
                await reconciler.ReconcileAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reconciliation pass failed; will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
