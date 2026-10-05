using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Drives the asynchronous parts of investing: it advances pending enrolments towards acceptance as
/// Upvest processes them, executes investments that have reached the threshold, and reconciles each
/// investment to the outcome of its order at Upvest (so the settled/failed status always reflects
/// reality even if a webhook is missed). It also registers the order webhook once at start-up.
/// </summary>
public class InvestingBackgroundService : BackgroundService
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
        _logger.LogInformation("InvestingBackgroundService started.");
        await RegisterWebhookAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var investing = scope.ServiceProvider.GetRequiredService<IInvestingService>();
                await investing.ProgressEnrolmentsAsync(stoppingToken);
                await investing.ProcessInvestmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Investing background tick failed; will retry. {Error}", ex.Message);
            }
        }
    }

    private async Task RegisterWebhookAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var upvest = scope.ServiceProvider.GetRequiredService<IUpvestClient>();
            await upvest.EnsureOrderWebhookAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not register the Upvest webhook at start-up: {Error}", ex.Message);
        }
    }
}
