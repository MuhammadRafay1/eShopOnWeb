using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Investing;

/// <summary>
/// Drives the investing capability's asynchronous work: it registers the Upvest webhook once at
/// start-up, then periodically reconciles against Upvest so enrolments activate, accounts are
/// provisioned, balances that have reached the threshold are invested, and each investment's
/// status reflects what actually happened at Upvest — even if no webhook is delivered.
/// </summary>
public class InvestingReconciliationService : BackgroundService
{
    private static readonly string[] WebhookEventTypes =
        { "USER", "ACCOUNT", "ACCOUNT_GROUP", "ORDER", "EXECUTION" };

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(4);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UpvestSettings _settings;
    private readonly ILogger<InvestingReconciliationService> _logger;

    public InvestingReconciliationService(
        IServiceScopeFactory scopeFactory,
        IOptions<UpvestSettings> settings,
        ILogger<InvestingReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the host a moment to start listening before registering the callback webhook.
        try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); } catch (OperationCanceledException) { return; }

        await TryRegisterWebhookAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var investing = scope.ServiceProvider.GetRequiredService<IInvestingService>();
                await investing.ProcessDueWorkAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Investing reconciliation tick failed: {Error}", ex.Message);
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task TryRegisterWebhookAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.CallbackBaseUrl))
        {
            _logger.LogWarning("No Upvest callback base url configured; skipping webhook registration (reconciliation still runs).");
            return;
        }

        var callbackUrl = _settings.CallbackBaseUrl.TrimEnd('/') + "/api/investing/upvest-webhook";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var upvest = scope.ServiceProvider.GetRequiredService<IUpvestClient>();
            await upvest.EnsureWebhookAsync(callbackUrl, WebhookEventTypes, cancellationToken);
            _logger.LogInformation("Upvest webhook ensured for {CallbackUrl}.", callbackUrl);
        }
        catch (Exception ex)
        {
            // Not fatal: the periodic reconciler keeps statuses correct even without webhooks.
            _logger.LogWarning("Could not register Upvest webhook ({Error}); relying on periodic reconciliation.", ex.Message);
        }
    }
}
