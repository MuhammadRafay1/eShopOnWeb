using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Periodically reconciles investing state against Upvest: advances pending enrolments to active or
/// rejected, invests balances that have reached the threshold (retrying anything an order-path attempt
/// could not complete), and settles pending investments to their final Upvest status. This guarantees
/// each investment's status in this application eventually reflects what happened at Upvest, even if a
/// webhook is never delivered.
/// </summary>
public class InvestmentReconciliationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvestmentReconciliationService> _logger;

    public InvestmentReconciliationService(
        IServiceScopeFactory scopeFactory,
        ILogger<InvestmentReconciliationService> logger)
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
                var investing = scope.ServiceProvider.GetRequiredService<IInvestingService>();
                await investing.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Investment reconciliation tick failed: {Message}", ex.Message);
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
