using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Periodically reconciles investing state with Upvest so enrolments complete,
/// ready balances invest, and investments settle without depending on webhook
/// delivery. Also registers the webhook subscription once at start (best-effort).
/// </summary>
public class InvestingReconciliationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<UpvestOptions> _options;
    private readonly ILogger<InvestingReconciliationBackgroundService> _logger;

    public InvestingReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<UpvestOptions> options,
        ILogger<InvestingReconciliationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RegisterWebhookAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reconciliation = scope.ServiceProvider.GetRequiredService<IInvestmentReconciliationService>();
                await reconciliation.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Investing reconciliation sweep failed; will retry.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task RegisterWebhookAsync(CancellationToken ct)
    {
        var baseUrl = _options.Value.CallbackBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var upvest = scope.ServiceProvider.GetRequiredService<IUpvestClient>();
            var callbackUrl = baseUrl.TrimEnd('/') + "/api/investing/upvest-webhook";
            var webhookId = await upvest.CreateEnabledWebhookAsync(callbackUrl, ct);
            if (webhookId is not null)
            {
                _logger.LogInformation("Registered Upvest webhook {WebhookId}.", webhookId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Upvest webhook registration skipped; reconciliation still runs on a timer.");
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
