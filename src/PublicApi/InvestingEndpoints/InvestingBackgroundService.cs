using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Drives investing forward off the request path: advances pending enrolments to accepted and provisions
/// their accounts, invests balances that have reached the threshold, and settles placed investments against
/// the provider. Runs on a short timer; each pass is idempotent and isolated from request handling.
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
                await processor.ProcessEnrolmentsAsync(stoppingToken);
                await processor.ProcessInvestmentsAsync(stoppingToken);
                await processor.ReconcileInvestmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a transient failure stop the loop.
                _logger.LogError(ex, "Investing background pass failed; will retry.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }
}
