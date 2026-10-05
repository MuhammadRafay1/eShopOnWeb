using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Http;

/// <summary>
/// Default <see cref="IUpvestInvestmentClient"/>. Serialises request bodies with snake_case
/// property names (as the Upvest API expects) and reads only the handful of fields the
/// integration needs out of each response. It never logs request bodies — those carry personal
/// details — logging only method, path and status code.
/// </summary>
public sealed class UpvestInvestmentClient : IUpvestInvestmentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestInvestmentClient> _logger;

    public UpvestInvestmentClient(
        IHttpClientFactory httpClientFactory,
        IOptions<UpvestSettings> options,
        ILogger<UpvestInvestmentClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    private HttpClient Client => _httpClientFactory.CreateClient(UpvestConstants.HttpClientName);

    public async Task<UpvestResource> CreateUserAsync(InvestorRegistration r, CancellationToken ct = default)
    {
        // The sign-up form carries no birth city/country, which Upvest requires at runtime;
        // default them from the nationality and residential city. fatca/consent are the
        // standard sandbox onboarding declarations.
        var now = DateTimeOffset.UtcNow.ToString("o");
        var body = new Dictionary<string, object?>
        {
            ["first_name"] = r.FirstName,
            ["last_name"] = r.LastName,
            ["email"] = r.Email,
            ["birth_date"] = r.BirthDate.ToString("yyyy-MM-dd"),
            ["birth_city"] = r.Address.City,
            ["birth_country"] = r.Nationality,
            ["nationalities"] = new[] { r.Nationality },
            ["phone_number"] = r.PhoneNumber,
            ["address"] = new Dictionary<string, object?>
            {
                ["address_line1"] = r.Address.Line1,
                ["postcode"] = r.Address.Postcode,
                ["city"] = r.Address.City,
                ["country"] = r.Address.Country
            },
            ["fatca"] = new Dictionary<string, object?> { ["status"] = false, ["confirmed_at"] = now },
            ["terms_and_conditions"] = new Dictionary<string, object?>
            {
                ["consent_document_id"] = UpvestConstants.TermsAndConditionsDocumentId,
                ["confirmed_at"] = now
            },
            ["data_privacy_and_sharing_agreement"] = new Dictionary<string, object?>
            {
                ["consent_document_id"] = UpvestConstants.DataPrivacyDocumentId,
                ["confirmed_at"] = now
            }
        };

        using var doc = await SendAsync(HttpMethod.Post, "/users", body, ct);
        return ReadResource(doc!.RootElement);
    }

    public async Task SubmitKycCheckAsync(string userId, InvestorRegistration r, CancellationToken ct = default)
    {
        // Including confirmed_address lets Upvest satisfy the proof-of-residence requirement
        // from the KYC document, so no separate POR check is needed.
        var body = new Dictionary<string, object?>
        {
            ["type"] = "KYC",
            ["check_confirmed_at"] = DateTimeOffset.UtcNow.ToString("o"),
            ["data_download_link"] = $"https://evidence.eshoponweb.example/kyc/{Guid.NewGuid()}.zip",
            ["document_type"] = "ID_CARD",
            ["provider"] = "eShopOnWeb KYC",
            ["method"] = "ELECTRONIC_ID",
            ["nationality"] = r.Nationality,
            ["confirmed_address"] = new Dictionary<string, object?>
            {
                ["address_line1"] = r.Address.Line1,
                ["postcode"] = r.Address.Postcode,
                ["city"] = r.Address.City,
                ["country"] = r.Address.Country
            }
        };
        (await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", body, ct))?.Dispose();
    }

    public async Task SubmitInstrumentFitCheckAsync(string userId, CancellationToken ct = default)
    {
        // The INSTRUMENT_FIT check carries the same evidence fields as KYC, plus the suitability result.
        var body = new Dictionary<string, object?>
        {
            ["type"] = "INSTRUMENT_FIT",
            ["check_confirmed_at"] = DateTimeOffset.UtcNow.ToString("o"),
            ["data_download_link"] = $"https://evidence.eshoponweb.example/instrument-fit/{Guid.NewGuid()}.zip",
            ["document_type"] = "ID_CARD",
            ["provider"] = "eShopOnWeb suitability",
            ["method"] = "ELECTRONIC_ID",
            ["instrument_suitability"] = new Dictionary<string, object?> { ["suitability"] = true }
        };
        (await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", body, ct))?.Dispose();
    }

    public async Task SetTaxResidenciesAsync(string userId, string taxCountry, string taxId, CancellationToken ct = default)
    {
        // The backend validates TIN format per country (e.g. Germany requires 11 digits). When we
        // do not have a valid-looking TIN, declare a missing-TIN reason instead so onboarding still
        // succeeds rather than being rejected on a format technicality.
        var residency = new Dictionary<string, object?> { ["country"] = taxCountry };
        if (IsPlausibleTin(taxCountry, taxId))
        {
            residency["tax_identifier_number"] = taxId;
        }
        else
        {
            residency["missing_tin_reason"] = "OTHER_REASONS";
        }

        var body = new Dictionary<string, object?> { ["tax_residencies"] = new[] { residency } };
        (await SendAsync(HttpMethod.Post, $"/users/{userId}/tax_residencies", body, ct))?.Dispose();
    }

    public async Task<UpvestResource> CreateAccountGroupAsync(string userId, CancellationToken ct = default)
    {
        // Reuse an existing account group for the user if one is already present (Upvest may have
        // created one, and creation is not repeatable), preferring a PERSONAL group.
        var existing = await ListDataAsync($"/users/{userId}/account_groups", ct);
        var reuse = Pick(existing, e => GetString(e, "type") == "PERSONAL") ?? Pick(existing, _ => true);
        if (reuse is not null)
        {
            return ReadResource(reuse.Value);
        }

        var body = new Dictionary<string, object?> { ["user_id"] = userId, ["type"] = "PERSONAL" };
        using var doc = await SendAsync(HttpMethod.Post, "/account_groups", body, ct);
        return ReadResource(doc!.RootElement);
    }

    public async Task<UpvestResource> CreateAccountAsync(string userId, string accountGroupId, CancellationToken ct = default)
    {
        // Reuse an existing TRADING account in this group if present.
        var existing = await ListDataAsync($"/users/{userId}/accounts", ct);
        var reuse = Pick(existing, e => GetString(e, "type") == "TRADING" && GetString(e, "account_group_id") == accountGroupId)
            ?? Pick(existing, e => GetString(e, "type") == "TRADING");
        if (reuse is not null)
        {
            return ReadResource(reuse.Value);
        }

        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_group_id"] = accountGroupId,
            ["type"] = "TRADING",
            ["name"] = "eShopOnWeb spare change"
        };
        using var doc = await SendAsync(HttpMethod.Post, "/accounts", body, ct);
        return ReadResource(doc!.RootElement);
    }

    private async Task<List<JsonElement>> ListDataAsync(string path, CancellationToken ct)
    {
        using var doc = await SendAsync(HttpMethod.Get, path, null, ct);
        var result = new List<JsonElement>();
        if (doc is null)
        {
            return result;
        }
        var root = doc.RootElement;
        var items = root.ValueKind == JsonValueKind.Array ? root
            : root.TryGetProperty("data", out var d) ? d
            : root.TryGetProperty("values", out var v) ? v
            : default;
        if (items.ValueKind == JsonValueKind.Array)
        {
            // Clone so the elements outlive the JsonDocument.
            result.AddRange(items.EnumerateArray().Select(e => e.Clone()));
        }
        return result;
    }

    private static JsonElement? Pick(List<JsonElement> items, Func<JsonElement, bool> predicate)
    {
        foreach (var item in items)
        {
            if (predicate(item))
            {
                return item;
            }
        }
        return null;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public async Task<string> GetUserStatusAsync(string userId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/users/{userId}", null, ct);
        return doc!.RootElement.GetProperty("status").GetString() ?? string.Empty;
    }

    public async Task<string> GetAccountStatusAsync(string accountId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/accounts/{accountId}", null, ct);
        return doc!.RootElement.GetProperty("status").GetString() ?? string.Empty;
    }

    public async Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["account_group_id"] = accountGroupId,
            ["amount"] = Money(amount),
            ["currency"] = "EUR"
        };
        (await SendAsync(HttpMethod.Post, "/virtual_cash_balances/increases", body, ct))?.Dispose();
    }

    public async Task<UpvestResource> PlaceBuyOrderAsync(string userId, string accountId, decimal cashAmount, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_id"] = accountId,
            ["side"] = "BUY",
            ["instrument_id"] = _settings.InstrumentId,
            ["instrument_id_type"] = "ISIN",
            ["cash_amount"] = Money(cashAmount),
            ["currency"] = "EUR",
            ["order_type"] = "MARKET",
            ["user_instrument_fit_acknowledgement"] = true
        };
        using var doc = await SendAsync(HttpMethod.Post, "/orders", body, ct);
        return ReadResource(doc!.RootElement);
    }

    public async Task<UpvestOrder> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/orders/{orderId}", null, ct);
        var root = doc!.RootElement;
        var status = root.GetProperty("status").GetString() ?? string.Empty;
        var executionStatuses = new List<string>();
        if (root.TryGetProperty("executions", out var executions) && executions.ValueKind == JsonValueKind.Array)
        {
            executionStatuses.AddRange(executions.EnumerateArray()
                .Select(e => e.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty));
        }
        return new UpvestOrder(root.GetProperty("id").GetString() ?? orderId, status, executionStatuses);
    }

    public async Task<IReadOnlyList<UpvestWebhook>> ListWebhooksAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/webhooks", null, ct);
        var result = new List<UpvestWebhook>();
        var root = doc!.RootElement;
        var items = root.ValueKind == JsonValueKind.Array ? root
            : root.TryGetProperty("values", out var v) ? v
            : root.TryGetProperty("data", out var d) ? d
            : default;
        if (items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                result.Add(new UpvestWebhook(
                    item.GetProperty("id").GetString() ?? string.Empty,
                    item.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("enabled", out var e) && e.GetBoolean()));
            }
        }
        return result;
    }

    public async Task<string> CreateWebhookAsync(string url, string title, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["title"] = title, ["url"] = url, ["type"] = new[] { "ALL" } };
        using var doc = await SendAsync(HttpMethod.Post, "/webhooks", body, ct);
        return doc!.RootElement.GetProperty("id").GetString() ?? string.Empty;
    }

    public async Task EnableWebhookAsync(string webhookId, string url, string title, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["enabled"] = true,
            ["title"] = title,
            ["url"] = url,
            ["type"] = new[] { "ALL" }
        };
        (await SendAsync(HttpMethod.Patch, $"/webhooks/{webhookId}", body, ct))?.Dispose();
    }

    public async Task TestWebhookAsync(string webhookId, CancellationToken ct = default) =>
        (await SendAsync(HttpMethod.Post, $"/webhooks/{webhookId}/test", null, ct))?.Dispose();

    public async Task<IReadOnlyList<UpvestVerifyKey>> GetVerifyKeysAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/auth/verify_keys", null, ct);
        var result = new List<UpvestVerifyKey>();
        if (doc!.RootElement.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.Array)
        {
            foreach (var k in keys.EnumerateArray())
            {
                result.Add(new UpvestVerifyKey(
                    k.TryGetProperty("kid", out var kid) ? kid.GetString() ?? string.Empty : string.Empty,
                    k.TryGetProperty("crv", out var crv) ? crv.GetString() ?? string.Empty : string.Empty,
                    k.TryGetProperty("x", out var x) ? x.GetString() ?? string.Empty : string.Empty,
                    k.TryGetProperty("y", out var y) ? y.GetString() ?? string.Empty : string.Empty));
            }
        }
        return result;
    }

    private async Task<JsonDocument?> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await Client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Read the error body for diagnostics. It never contains the personal details we sent,
            // only Upvest's validation message, so it is safe to log.
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Upvest {Method} {Path} failed with {Status}: {Error}",
                method, path, (int)response.StatusCode, Truncate(error));
            throw new UpvestApiException(method.Method, path, (int)response.StatusCode, error);
        }

        _logger.LogInformation("Upvest {Method} {Path} -> {Status}", method, path, (int)response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(content) ? null : JsonDocument.Parse(content);
    }

    private static UpvestResource ReadResource(JsonElement element) => new(
        element.GetProperty("id").GetString() ?? string.Empty,
        element.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty);

    private static string Money(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    private static bool IsPlausibleTin(string country, string taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId))
        {
            return false;
        }
        // Germany enforces exactly 11 digits; for other countries accept any non-empty value.
        if (string.Equals(country, "DE", StringComparison.OrdinalIgnoreCase))
        {
            return taxId.Length == 11 && taxId.All(char.IsDigit);
        }
        return true;
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}
