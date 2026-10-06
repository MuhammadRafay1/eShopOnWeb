using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Registers this application's webhook subscription with Upvest once at startup, pointing at the
/// configured public callback address, so Upvest can notify us of KYC and order state changes.
/// Best-effort: failures are logged and never prevent startup, because the reconciler is the
/// authoritative settlement path regardless of webhook delivery.
/// </summary>
public sealed class UpvestWebhookRegistrar : BackgroundService
{
    public const string WebhookPath = "/api/investing/upvest-webhook";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestWebhookRegistrar> _logger;

    public UpvestWebhookRegistrar(IHttpClientFactory httpClientFactory, IOptions<UpvestSettings> settings, ILogger<UpvestWebhookRegistrar> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.CallbackBaseUrl))
        {
            _logger.LogInformation("No Upvest callback base URL configured; skipping webhook registration.");
            return;
        }

        var webhookUrl = _settings.CallbackBaseUrl.TrimEnd('/') + WebhookPath;

        try
        {
            var client = _httpClientFactory.CreateClient(UpvestHttpClient.Name);

            if (await WebhookExistsAsync(client, webhookUrl, stoppingToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Upvest webhook subscription already present.");
                return;
            }

            var payload = new
            {
                title = "eShopOnWeb investing",
                url = webhookUrl,
                type = new[] { "USER_CHECK", "ORDER", "ORDER_CANCELLATION" },
            };

            using var response = await client.PostAsJsonAsync("webhooks", payload, stoppingToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Registered Upvest webhook subscription.");
            }
            else
            {
                _logger.LogWarning("Upvest webhook registration returned {StatusCode}; relying on reconciliation.", (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Upvest webhook registration failed ({Error}); relying on reconciliation.", ex.Message);
        }
    }

    private static async Task<bool> WebhookExistsAsync(HttpClient client, string webhookUrl, CancellationToken token)
    {
        using var response = await client.GetAsync("webhooks", token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return false;

        await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);

        var root = doc.RootElement;
        var list = root.ValueKind == JsonValueKind.Array ? root
            : root.TryGetProperty("webhooks", out var w) ? w
            : root.TryGetProperty("data", out var d) ? d
            : default;

        if (list.ValueKind != JsonValueKind.Array) return false;
        return list.EnumerateArray().Any(e =>
            e.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String &&
            string.Equals(u.GetString(), webhookUrl, StringComparison.OrdinalIgnoreCase));
    }
}
