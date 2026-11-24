using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Upvest;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// <see cref="IUpvestClient"/> over the Upvest Investment API. Each call acquires a token for the
/// required scope, sends a signed request against <c>Upvest:BaseUrl</c>, and maps the response to a
/// domain contract. Request bodies (which may contain personal data) are never logged.
/// </summary>
public sealed class UpvestClient : IUpvestClient
{
    private static class Scopes
    {
        public const string UsersAdmin = "users:admin";
        public const string ChecksAdmin = "checks:admin";
        public const string AccountsAdmin = "accounts:admin";
        public const string AccountsRead = "accounts:read";
        public const string PortfoliosAdmin = "portfolios:admin";
        public const string PortfoliosRead = "portfolios:read";
        public const string OrdersAdmin = "orders:admin";
        public const string TopUpsAdmin = "topups:admin";
        public const string ValuationsRead = "valuations:read";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestTokenProvider _tokenProvider;
    private readonly UpvestSettings _settings;

    public UpvestClient(IHttpClientFactory httpClientFactory, UpvestTokenProvider tokenProvider, IOptions<UpvestSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _settings = settings.Value;
    }

    // ------------------------------------------------------------------ Users

    public async Task<UpvestUser> CreateUserAsync(UpvestCreateUserRequest request, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["first_name"] = request.FirstName,
            ["last_name"] = request.LastName,
            ["birth_date"] = request.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["nationalities"] = new[] { request.Nationality },
            ["address"] = AddressBody(request.Address),
            ["fatca"] = new Dictionary<string, object?>
            {
                ["status"] = false,
                ["confirmed_at"] = Rfc3339Now()
            }
        };
        AddIfPresent(body, "email", request.Email);
        AddIfPresent(body, "birth_city", request.BirthCity);
        AddIfPresent(body, "birth_country", request.BirthCountry);
        var phone = DigitsOnly(request.PhoneNumber);
        AddIfPresent(body, "phone_number", phone.Length >= 8 ? phone : null);

        using var doc = await SendAsync(HttpMethod.Post, "/users", Scopes.UsersAdmin, body, ct);
        var root = doc!.RootElement;
        return new UpvestUser(root.GetProperty("id").GetString()!, GetString(root, "status") ?? "INACTIVE");
    }

    public async Task<UpvestUser?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        using var doc = await GetOrNullAsync($"/users/{userId}", Scopes.UsersAdmin, ct);
        if (doc is null) return null;
        var root = doc.RootElement;
        return new UpvestUser(root.GetProperty("id").GetString()!, GetString(root, "status") ?? "INACTIVE");
    }

    public async Task<string> CreateKycCheckAsync(string userId, UpvestKycCheckRequest request, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "KYC",
            ["check_confirmed_at"] = Rfc3339Now(),
            ["data_download_link"] = "https://eshoponweb.example/kyc-evidence/not-applicable",
            ["document_type"] = request.DocumentType,
            ["document_expiration_date"] = request.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["nationality"] = request.IssuingCountry,
            ["provider"] = "eShopOnWeb",
            ["method"] = "ELECTRONIC_ID",
            ["confirmed_address"] = AddressBody(request.Address)
        };

        using var doc = await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", Scopes.ChecksAdmin, body, ct);
        return doc!.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<UpvestCheck?> GetCheckAsync(string userId, string checkId, CancellationToken ct = default)
    {
        using var doc = await GetOrNullAsync($"/users/{userId}/checks/{checkId}", Scopes.ChecksAdmin, ct);
        if (doc is null) return null;
        var root = doc.RootElement;
        return new UpvestCheck(root.GetProperty("id").GetString()!, GetString(root, "status") ?? "IN_PROGRESS");
    }

    // --------------------------------------------------------------- Accounts

    public async Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["type"] = "PERSONAL"
        };
        using var doc = await SendAsync(HttpMethod.Post, "/account_groups", Scopes.AccountsAdmin, body, ct);
        var root = doc!.RootElement;
        return new UpvestAccountGroup(root.GetProperty("id").GetString()!, GetString(root, "status") ?? "PENDING_APPROVAL");
    }

    public async Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_group_id"] = accountGroupId,
            ["type"] = "PORTFOLIO"
        };
        AddIfPresent(body, "business_id", _settings.BusinessId);

        using var doc = await SendAsync(HttpMethod.Post, "/accounts", Scopes.AccountsAdmin, body, ct);
        return MapAccount(doc!.RootElement);
    }

    public async Task<UpvestAccount?> GetAccountAsync(string accountId, CancellationToken ct = default)
    {
        using var doc = await GetOrNullAsync($"/accounts/{accountId}", Scopes.AccountsRead, ct);
        return doc is null ? null : MapAccount(doc.RootElement);
    }

    private static UpvestAccount MapAccount(JsonElement root) => new(
        root.GetProperty("id").GetString()!,
        GetString(root, "account_group_id") ?? string.Empty,
        GetString(root, "status") ?? "PENDING_APPROVAL");

    // ---------------------------------------------- Allocations & configuration

    public Task<UpvestAllocation> CreateAllocationAsync(string name, IReadOnlyList<UpvestAllocationEntry> funds, CancellationToken ct = default)
        => UpsertAllocationAsync(HttpMethod.Post, "/portfolios/allocations", name, funds, ct);

    public Task<UpvestAllocation> UpdateAllocationAsync(string allocationId, string name, IReadOnlyList<UpvestAllocationEntry> funds, CancellationToken ct = default)
        => UpsertAllocationAsync(HttpMethod.Put, $"/portfolios/allocations/{allocationId}", name, funds, ct);

    private async Task<UpvestAllocation> UpsertAllocationAsync(HttpMethod method, string path, string name, IReadOnlyList<UpvestAllocationEntry> funds, CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["allocation"] = funds.Select(f => new Dictionary<string, object?>
            {
                ["instrument_id"] = f.Isin,
                ["instrument_id_type"] = "ISIN",
                ["weight"] = FormatWeight(f.Weight)
            }).ToArray()
        };

        using var doc = await SendAsync(method, path, Scopes.PortfoliosAdmin, body, ct);
        var root = doc!.RootElement;
        var entries = new List<UpvestAllocationEntry>();
        if (root.TryGetProperty("allocation", out var alloc) && alloc.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in alloc.EnumerateArray())
            {
                string isin = GetString(e, "instrument_id") ?? string.Empty;
                decimal weight = ParseDecimal(GetString(e, "weight"));
                entries.Add(new UpvestAllocationEntry(isin, weight));
            }
        }
        return new UpvestAllocation(root.GetProperty("id").GetString()!, GetString(root, "name") ?? name, entries);
    }

    public async Task CreateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["account_id"] = accountId,
            ["allocation_id"] = allocationId
        };
        using var _ = await SendAsync(HttpMethod.Post, "/portfolios/configurations", Scopes.PortfoliosAdmin, body, ct);
    }

    public async Task UpdateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["allocation_id"] = allocationId };
        using var _ = await SendAsync(HttpMethod.Patch, $"/portfolios/configurations/{accountId}", Scopes.PortfoliosAdmin, body, ct);
    }

    // --------------------------------------------------------------- Paying in

    public async Task TopUpAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["account_group_id"] = accountGroupId,
            ["cash_amount"] = FormatAmount(amount),
            ["currency"] = currency
        };
        using var _ = await SendAsync(HttpMethod.Post, "/payments/topups", Scopes.TopUpsAdmin, body, ct);
    }

    public async Task<UpvestOrder> CreatePortfolioOrderAsync(string userId, string accountId, decimal amount, string currency, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_id"] = accountId,
            ["cash_amount"] = FormatAmount(amount),
            ["currency"] = currency,
            ["side"] = "BUY"
        };
        using var doc = await SendAsync(HttpMethod.Post, "/portfolios/orders", $"{Scopes.OrdersAdmin} {Scopes.PortfoliosAdmin}", body, ct);
        return MapOrder(doc!.RootElement);
    }

    public async Task<UpvestOrder?> GetPortfolioOrderAsync(string portfolioOrderId, CancellationToken ct = default)
    {
        using var doc = await GetOrNullAsync($"/portfolios/orders/{portfolioOrderId}", Scopes.PortfoliosRead, ct);
        return doc is null ? null : MapOrder(doc.RootElement);
    }

    private static UpvestOrder MapOrder(JsonElement root)
    {
        string id = GetString(root, "id") ?? GetString(root, "portfolio_order_id") ?? string.Empty;
        return new UpvestOrder(id, GetString(root, "status") ?? "NEW", GetString(root, "cancellation_reason"));
    }

    // ------------------------------------------------------------- Rebalancing

    public async Task<string> TriggerRebalancingAsync(string allocationId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["allocations"] = new[] { allocationId } };
        using var doc = await SendAsync(HttpMethod.Post, "/portfolios/rebalancings/executions", Scopes.PortfoliosAdmin, body, ct);
        return doc!.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<UpvestRebalancing?> GetRebalancingAsync(string executionId, CancellationToken ct = default)
    {
        // The execution resource itself carries no status; it is derived from its execution orders.
        using var doc = await GetOrNullAsync($"/portfolios/rebalancings/executions/{executionId}", Scopes.PortfoliosRead, ct);
        if (doc is null) return null;

        var orders = new List<UpvestOrder>();
        using var ordersDoc = await GetOrNullAsync($"/portfolios/rebalancings/executions/{executionId}/orders", Scopes.PortfoliosRead, ct);
        if (ordersDoc is not null && ordersDoc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var o in data.EnumerateArray())
                orders.Add(new UpvestOrder(GetString(o, "id") ?? string.Empty, GetString(o, "status") ?? "NEW", GetString(o, "cancellation_reason")));
        }
        return new UpvestRebalancing(executionId, orders);
    }

    // ---------------------------------------------------------------- Valuation

    public async Task<UpvestValuation?> GetAccountValuationAsync(string accountId, CancellationToken ct = default)
    {
        using var doc = await GetOrNullAsync($"/accounts/{accountId}/valuations/current?price_quality=HIGHEST_AVAILABLE", Scopes.ValuationsRead, ct);
        if (doc is null) return null;
        var root = doc.RootElement;

        decimal total = 0m;
        string currency = "EUR";
        if (root.TryGetProperty("total_security_value", out var tsv))
        {
            total = ParseDecimal(GetString(tsv, "amount"));
            currency = GetString(tsv, "currency") ?? currency;
        }

        var holdings = new List<UpvestHolding>();
        if (root.TryGetProperty("security_positions", out var positions) && positions.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in positions.EnumerateArray())
            {
                string isin = string.Empty;
                if (p.TryGetProperty("instrument", out var instr))
                    isin = GetString(instr, "isin") ?? GetString(instr, "uuid") ?? string.Empty;
                decimal quantity = ParseDecimal(GetString(p, "quantity"));
                decimal value = 0m;
                if (p.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Object)
                    value = ParseDecimal(GetString(v, "amount"));
                holdings.Add(new UpvestHolding(isin, quantity, value));
            }
        }
        return new UpvestValuation(holdings, total, currency);
    }

    // ----------------------------------------------------------------- Plumbing

    private async Task<JsonDocument?> GetOrNullAsync(string path, string scope, CancellationToken ct)
    {
        try
        {
            return await SendAsync(HttpMethod.Get, path, scope, null, ct);
        }
        catch (UpvestApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<JsonDocument?> SendAsync(HttpMethod method, string path, string scope, object? body, CancellationToken ct)
    {
        string token = await _tokenProvider.GetTokenAsync(scope, ct);
        var client = _httpClientFactory.CreateClient(UpvestHttpClient.Name);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        request.Headers.TryAddWithoutValidation("authorization", "Bearer " + token);
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);
        if (!string.IsNullOrWhiteSpace(_settings.ApiVersion))
            request.Headers.TryAddWithoutValidation("upvest-api-version", _settings.ApiVersion);
        if (method != HttpMethod.Get)
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());

        if (body is not null)
        {
            string json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }

        using var response = await client.SendAsync(request, ct);
        string payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw UpvestErrors.FromResponse((int)response.StatusCode, payload, $"Upvest {method} {path} failed");

        return string.IsNullOrWhiteSpace(payload) ? null : JsonDocument.Parse(payload);
    }

    // ------------------------------------------------------------------ Helpers

    private static Dictionary<string, object?> AddressBody(UpvestAddress a) => new()
    {
        ["address_line1"] = a.Line1,
        ["postcode"] = a.Postcode,
        ["city"] = a.City,
        ["country"] = a.Country
    };

    private static void AddIfPresent(IDictionary<string, object?> dict, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) dict[key] = value;
    }

    private static string DigitsOnly(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

    private static string Rfc3339Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatWeight(decimal weight) => weight.ToString("0.############", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? (value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString())
            : null;
}
