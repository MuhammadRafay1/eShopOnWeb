using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Investing.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Registers (and activates, and test-fires) this application's webhook subscription with Upvest
/// so settlement and acceptance events arrive in real time. It runs once the host has started —
/// so the callback endpoint is already listening when the test event is delivered — and is fully
/// idempotent: an existing subscription for our callback URL is reused and simply re-enabled.
///
/// Registration is best-effort: if it fails (e.g. the callback is not reachable from the sandbox),
/// the periodic reconciliation sweep still keeps every shopper's state correct.
/// </summary>
public sealed class UpvestWebhookRegistrar : BackgroundService
{
    private readonly IUpvestInvestmentClient _upvest;
    private readonly UpvestSettings _settings;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<UpvestWebhookRegistrar> _logger;

    public UpvestWebhookRegistrar(
        IUpvestInvestmentClient upvest,
        IOptions<UpvestSettings> options,
        IHostApplicationLifetime lifetime,
        ILogger<UpvestWebhookRegistrar> logger)
    {
        _upvest = upvest;
        _settings = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitForApplicationStartedAsync(stoppingToken);
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var callbackUrl = _settings.CallbackBaseUrl.TrimEnd('/') + UpvestWebhooks.Path;

        try
        {
            var existing = (await _upvest.ListWebhooksAsync(stoppingToken))
                .FirstOrDefault(w => string.Equals(w.Url, callbackUrl, StringComparison.OrdinalIgnoreCase));

            var webhookId = existing?.Id;
            if (webhookId is null)
            {
                webhookId = await _upvest.CreateWebhookAsync(callbackUrl, UpvestWebhooks.SubscriptionTitle, stoppingToken);
                _logger.LogInformation("Created Upvest webhook {WebhookId} -> {Url}.", webhookId, callbackUrl);
            }

            await _upvest.EnableWebhookAsync(webhookId, callbackUrl, UpvestWebhooks.SubscriptionTitle, stoppingToken);

            try
            {
                await _upvest.TestWebhookAsync(webhookId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Upvest webhook test event could not be delivered; reconciliation will cover settlement.");
            }

            _logger.LogInformation("Upvest webhook {WebhookId} is active.", webhookId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not register the Upvest webhook; reconciliation will keep state in sync.");
        }
    }

    private async Task WaitForApplicationStartedAsync(CancellationToken stoppingToken)
    {
        var tcs = new TaskCompletionSource();
        using var _ = _lifetime.ApplicationStarted.Register(() => tcs.TrySetResult());
        using var __ = stoppingToken.Register(() => tcs.TrySetResult());
        await tcs.Task;
    }
}
