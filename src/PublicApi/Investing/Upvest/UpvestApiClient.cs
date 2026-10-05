using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Investing.Upvest;

public sealed class UpvestApiClient : IUpvestApiClient
{
    private readonly HttpClient _http;
    private readonly UpvestOptions _options;
    private readonly ILogger<UpvestApiClient> _logger;

    public UpvestApiClient(HttpClient http, IOptions<UpvestOptions> options, ILogger<UpvestApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    private static string NowRfc3339() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    public async Task<UpvestUser> CreateUserAsync(EnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["first_name"] = details.FirstName,
            ["last_name"] = details.LastName,
            ["email"] = details.Email,
            ["birth_date"] = details.BirthDate,
            ["nationalities"] = new[] { details.Nationality },
            ["phone_number"] = string.IsNullOrWhiteSpace(details.PhoneNumber) ? null : details.PhoneNumber,
            ["address"] = new Dictionary<string, object?>
            {
                ["address_line1"] = details.Address.Line1,
                ["postcode"] = details.Address.Postcode,
                ["city"] = details.Address.City,
                ["country"] = details.Address.Country
            },
            ["fatca"] = new Dictionary<string, object?>
            {
                ["status"] = false,
                ["confirmed_at"] = NowRfc3339()
            }
        };

        using var doc = await SendAsync(HttpMethod.Post, "/users", body, cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetString()!;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "INACTIVE" : "INACTIVE";
        return new UpvestUser(id, status);
    }

    public async Task SubmitKycCheckAsync(string userId, EnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "KYC",
            ["check_confirmed_at"] = NowRfc3339(),
            ["data_download_link"] = $"https://eshoponweb.invalid/kyc/{userId}.zip",
            ["document_type"] = "ID_CARD",
            ["provider"] = "eShopOnWeb",
            ["method"] = "ELECTRONIC_ID",
            ["nationality"] = details.Nationality,
            ["confirmed_address"] = new Dictionary<string, object?>
            {
                ["address_line1"] = details.Address.Line1,
                ["postcode"] = details.Address.Postcode,
                ["city"] = details.Address.City,
                ["country"] = details.Address.Country
            }
        };
        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", body, cancellationToken).ConfigureAwait(false);
    }

    public async Task SubmitInstrumentFitCheckAsync(string userId, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "INSTRUMENT_FIT",
            ["check_confirmed_at"] = NowRfc3339(),
            ["instrument_suitability"] = new Dictionary<string, object?> { ["suitability"] = true },
            // The sandbox validates these common check fields on every check type.
            ["data_download_link"] = $"https://eshoponweb.invalid/instrument-fit/{userId}.zip",
            ["document_type"] = "ID_CARD",
            ["provider"] = "eShopOnWeb",
            ["method"] = "ELECTRONIC_ID"
        };
        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", body, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetTaxResidenciesAsync(string userId, EnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["tax_residencies"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["country"] = details.TaxCountry,
                    ["tax_identifier_number"] = details.TaxId
                }
            }
        };
        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/tax_residencies", body, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateNationalIdentifierAsync(string userId, EnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "NATIONAL_ID",
            ["issuing_country"] = details.Nationality,
            ["identifier"] = details.TaxId
        };
        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/identifiers", body, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> CreateAccountGroupAsync(string userId, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["user_id"] = userId, ["type"] = "PERSONAL" };
        using var doc = await SendAsync(HttpMethod.Post, "/account_groups", body, cancellationToken).ConfigureAwait(false);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<string> CreateAccountAsync(string userId, string accountGroupId, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_group_id"] = accountGroupId,
            ["type"] = "TRADING",
            ["name"] = "Spare change"
        };
        using var doc = await SendAsync(HttpMethod.Post, "/accounts", body, cancellationToken).ConfigureAwait(false);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<string?> FindAccountGroupIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/users/{userId}/account_groups", null, cancellationToken).ConfigureAwait(false);
        return FirstId(doc.RootElement);
    }

    public async Task<string?> FindTradingAccountIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/users/{userId}/accounts", null, cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (type is null || string.Equals(type, "TRADING", StringComparison.OrdinalIgnoreCase))
                {
                    if (item.TryGetProperty("id", out var id))
                    {
                        return id.GetString();
                    }
                }
            }
        }
        return null;
    }

    private static string? FirstId(JsonElement root)
    {
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var id))
                {
                    return id.GetString();
                }
            }
        }
        return null;
    }

    public async Task<string> GetUserStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/users/{userId}", null, cancellationToken).ConfigureAwait(false);
        return doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "INACTIVE" : "INACTIVE";
    }

    public async Task<string> GetAccountStatusAsync(string accountId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/accounts/{accountId}", null, cancellationToken).ConfigureAwait(false);
        return doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "PENDING_APPROVAL" : "PENDING_APPROVAL";
    }

    public async Task FundAccountGroupAsync(string accountGroupId, decimal amount, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["account_group_id"] = accountGroupId,
            ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = "EUR"
        };
        using var _ = await SendAsync(HttpMethod.Post, "/virtual_cash_balances/increases", body, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> PlaceBuyOrderAsync(string userId, string accountId, decimal cashAmount, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_id"] = accountId,
            ["side"] = "BUY",
            ["instrument_id"] = _options.InstrumentId,
            ["instrument_id_type"] = "ISIN",
            ["order_type"] = "MARKET",
            ["cash_amount"] = cashAmount.ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = "EUR",
            ["user_instrument_fit_acknowledgement"] = true
        };
        using var doc = await SendAsync(HttpMethod.Post, "/orders", body, cancellationToken).ConfigureAwait(false);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<UpvestOrderSnapshot> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/orders/{orderId}", null, cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "NEW" : "NEW";
        var execs = new List<string>();
        if (root.TryGetProperty("executions", out var e) && e.ValueKind == JsonValueKind.Array)
        {
            foreach (var exec in e.EnumerateArray())
            {
                if (exec.TryGetProperty("status", out var es))
                {
                    execs.Add(es.GetString() ?? string.Empty);
                }
            }
        }
        return new UpvestOrderSnapshot(orderId, status, execs);
    }

    public async Task EnsureWebhookAsync(string callbackUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using (var list = await SendAsync(HttpMethod.Get, "/webhooks", null, cancellationToken).ConfigureAwait(false))
            {
                if (list.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var wh in data.EnumerateArray())
                    {
                        if (wh.TryGetProperty("url", out var u) && string.Equals(u.GetString(), callbackUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            return; // already registered
                        }
                    }
                }
            }

            var create = new Dictionary<string, object?>
            {
                ["title"] = "eShopOnWeb investing",
                ["url"] = callbackUrl,
                ["type"] = new[] { "ALL" }
            };
            string webhookId;
            using (var created = await SendAsync(HttpMethod.Post, "/webhooks", create, cancellationToken).ConfigureAwait(false))
            {
                webhookId = created.RootElement.GetProperty("id").GetString()!;
            }

            var enable = new Dictionary<string, object?> { ["enabled"] = true };
            using var _ = await SendAsync(HttpMethod.Patch, $"/webhooks/{webhookId}", enable, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Registered Upvest webhook subscription.");
        }
        catch (Exception ex)
        {
            // Webhooks are best-effort; reconciliation polling keeps state correct regardless.
            _logger.LogWarning(ex, "Could not register Upvest webhook subscription; relying on reconciliation polling.");
        }
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = UpvestContent.Json(body);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Log only the method, path and status — never the request body (it may carry personal data).
            _logger.LogError("Upvest {Method} {Path} failed with status {Status}.", method, path, (int)response.StatusCode);
            throw new UpvestException($"Upvest {method} {path} failed with status {(int)response.StatusCode}.", response.StatusCode);
        }

        if (content.Length == 0)
        {
            return JsonDocument.Parse("{}");
        }
        return JsonDocument.Parse(content);
    }
}
