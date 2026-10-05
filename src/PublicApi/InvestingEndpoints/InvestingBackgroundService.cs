using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Drives "invest your change" off the request path: it subscribes the Upvest webhook once at startup, then
/// periodically reconciles every pending enrolment and investment against authoritative Upvest state. This
/// is what carries enrolments to active and investments to settled/failed, independently of webhook delivery.
/// </summary>
public sealed class InvestingBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUpvestGateway _gateway;
    private readonly UpvestOptions _options;
    private readonly ILogger<InvestingBackgroundService> _logger;

    public InvestingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IUpvestGateway gateway,
        UpvestOptions options,
        ILogger<InvestingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _gateway = gateway;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureWebhookAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<InvestingReconciler>();
                await reconciler.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Investing reconcile pass failed: {Error}", ex.GetType().Name);
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task EnsureWebhookAsync(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 5 && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                await _gateway.EnsureWebhookAsync(_options.WebhookUrl, ct);
                _logger.LogInformation("Upvest webhook subscription ensured for {Url}.", _options.WebhookUrl);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not ensure Upvest webhook (attempt {Attempt}): {Error}. Reconciliation will still proceed by polling.", attempt, ex.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(3), ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
