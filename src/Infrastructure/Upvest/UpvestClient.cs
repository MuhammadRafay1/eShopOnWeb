using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Talks to the Upvest Investment API. Authentication (OAuth bearer + HTTP message signature) is
/// handled entirely by <see cref="UpvestAuthenticationHandler"/> on the underlying
/// <see cref="HttpClient"/>; this class only builds and interprets payloads.
/// </summary>
public sealed class UpvestClient : IUpvestClient
{
    // Example consent document ids provided by Upvest for Sandbox onboarding.
    private const string SandboxTermsConsentId = "d0b83880-3809-4eee-b0df-ca50db84c15a";
    private const string SandboxDataPrivacyConsentId = "7ab0fc5c-8157-4acd-b02d-6ccda0e81dec";

    private readonly HttpClient _http;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestClient> _logger;

    public UpvestClient(HttpClient http, UpvestSettings settings, ILogger<UpvestClient> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string> CreateInvestorAsync(UpvestInvestorRegistration r, CancellationToken ct)
    {
        var now = Timestamp();
        var address = new Dictionary<string, object>
        {
            ["address_line1"] = r.AddressLine1,
            ["postcode"] = r.Postcode,
            ["city"] = r.City,
            ["country"] = r.Country,
        };

        var userPayload = new Dictionary<string, object>
        {
            ["first_name"] = r.FirstName,
            ["last_name"] = r.LastName,
            ["email"] = r.Email,
            ["birth_date"] = r.BirthDate,
            ["nationalities"] = new[] { r.Nationality },
            ["phone_number"] = r.PhoneNumber,
            ["address"] = address,
            ["fatca"] = new Dictionary<string, object> { ["status"] = false, ["confirmed_at"] = now },
            ["terms_and_conditions"] = new Dictionary<string, object> { ["consent_document_id"] = SandboxTermsConsentId, ["confirmed_at"] = now },
            ["data_privacy_and_sharing_agreement"] = new Dictionary<string, object> { ["consent_document_id"] = SandboxDataPrivacyConsentId, ["confirmed_at"] = now },
        };

        var user = await PostAsync("/users", userPayload, ct);
        var userId = GetString(user, "id") ?? throw new HttpRequestException("Upvest did not return a user id.");

        var checkPayload = new Dictionary<string, object>
        {
            ["type"] = "KYC",
            ["check_confirmed_at"] = now,
            ["data_download_link"] = $"{_settings.CallbackBaseUrl.TrimEnd('/')}/kyc/{userId}.zip",
            ["document_type"] = "ID_CARD",
            ["document_expiration_date"] = "2035-01-01",
            ["nationality"] = r.Nationality,
            ["provider"] = "eShopOnWeb onboarding",
            ["method"] = "VIDEO_ID",
            ["confirmed_address"] = address,
        };
        await PostAsync($"/users/{userId}/checks", checkPayload, ct);

        var taxPayload = new Dictionary<string, object>
        {
            ["tax_residencies"] = new[]
            {
                new Dictionary<string, object> { ["country"] = r.TaxCountry, ["tax_identifier_number"] = r.TaxId }
            }
        };
        await PostAsync($"/users/{userId}/tax_residencies", taxPayload, ct);

        return userId;
    }

    public async Task<string> GetUserStatusAsync(string upvestUserId, CancellationToken ct)
    {
        var user = await GetAsync($"/users/{upvestUserId}", ct);
        return GetString(user, "status") ?? "UNKNOWN";
    }

    public async Task<UpvestAccount> CreateTradingAccountAsync(string upvestUserId, CancellationToken ct)
    {
        var groupPayload = new Dictionary<string, object> { ["user_id"] = upvestUserId, ["type"] = "PERSONAL" };
        var group = await PostAsync("/account_groups", groupPayload, ct);
        var accountGroupId = GetString(group, "id") ?? throw new HttpRequestException("Upvest did not return an account group id.");

        var accountPayload = new Dictionary<string, object>
        {
            ["user_id"] = upvestUserId,
            ["account_group_id"] = accountGroupId,
            ["type"] = "TRADING",
            ["name"] = "Spare change",
        };
        var account = await PostAsync("/accounts", accountPayload, ct);
        var accountId = GetString(account, "id") ?? throw new HttpRequestException("Upvest did not return an account id.");
        var status = GetString(account, "status") ?? "PENDING_APPROVAL";

        return new UpvestAccount(accountGroupId, accountId, status);
    }

    public async Task<string> GetAccountStatusAsync(string accountId, CancellationToken ct)
    {
        var account = await GetAsync($"/accounts/{accountId}", ct);
        return GetString(account, "status") ?? "UNKNOWN";
    }

    public async Task TopUpAsync(string accountGroupId, decimal amount, CancellationToken ct)
    {
        var payload = new Dictionary<string, object>
        {
            ["account_group_id"] = accountGroupId,
            ["cash_amount"] = Money(amount),
            ["currency"] = "EUR",
        };
        await PostAsync("/payments/topups", payload, ct);
    }

    public async Task<string> PlaceBuyOrderAsync(string upvestUserId, string accountId, decimal amount, CancellationToken ct)
    {
        var payload = new Dictionary<string, object>
        {
            ["user_id"] = upvestUserId,
            ["account_id"] = accountId,
            ["cash_amount"] = Money(amount),
            ["currency"] = "EUR",
            ["side"] = "BUY",
            ["instrument_id"] = _settings.InstrumentId,
            ["instrument_id_type"] = "ISIN",
            ["order_type"] = "MARKET",
            ["user_instrument_fit_acknowledgement"] = true,
        };
        var order = await PostAsync("/orders", payload, ct);
        return GetString(order, "id") ?? throw new HttpRequestException("Upvest did not return an order id.");
    }

    public async Task<string> GetOrderStatusAsync(string orderId, CancellationToken ct)
    {
        var order = await GetAsync($"/orders/{orderId}", ct);
        return GetString(order, "status") ?? "UNKNOWN";
    }

    public async Task EnsureOrderWebhookAsync(CancellationToken ct)
    {
        var callbackUrl = $"{_settings.CallbackBaseUrl.TrimEnd('/')}/api/investing/upvest/webhook";
        try
        {
            var existing = await GetAsync("/webhooks", ct);
            if (existing.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var hook in data.EnumerateArray())
                {
                    if (GetString(hook, "url") == callbackUrl)
                    {
                        if (hook.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.True)
                            return;
                        var id = GetString(hook, "id");
                        if (id is not null) await EnableWebhookAsync(id, callbackUrl, ct);
                        return;
                    }
                }
            }

            var payload = new Dictionary<string, object>
            {
                ["title"] = "eShopOnWeb investing - order events",
                ["url"] = callbackUrl,
                ["type"] = new[] { "ORDER", "EXECUTION" },
                ["config"] = new Dictionary<string, object> { ["delay"] = "1s", ["max_package_size"] = 51200 },
            };
            var created = await PostAsync("/webhooks", payload, ct);
            var newId = GetString(created, "id");
            if (newId is not null) await EnableWebhookAsync(newId, callbackUrl, ct);
            _logger.LogInformation("Registered Upvest order webhook at {Url}.", callbackUrl);
        }
        catch (Exception ex)
        {
            // Settlement also reconciles by polling, so webhook registration is best-effort.
            _logger.LogWarning("Could not register Upvest order webhook ({Error}); settlement will rely on polling.", ex.Message);
        }
    }

    private async Task EnableWebhookAsync(string webhookId, string callbackUrl, CancellationToken ct)
    {
        var payload = new Dictionary<string, object>
        {
            ["title"] = "eShopOnWeb investing - order events",
            ["url"] = callbackUrl,
            ["type"] = new[] { "ORDER", "EXECUTION" },
            ["enabled"] = true,
            ["config"] = new Dictionary<string, object> { ["delay"] = "1s", ["max_package_size"] = 51200 },
        };
        await SendAsync(HttpMethod.Patch, $"/webhooks/{webhookId}", payload, ct);
    }

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private Task<JsonElement> PostAsync(string path, object payload, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, path, payload, ct);

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        }
        return await ReadAsync(request, ct);
    }

    private async Task<JsonElement> GetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await ReadAsync(request, ct);
    }

    private async Task<JsonElement> ReadAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Upvest {request.Method} {request.RequestUri?.AbsolutePath} failed with status {(int)response.StatusCode}.");

        if (string.IsNullOrWhiteSpace(body))
            return default;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? (value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString())
            : null;
}
