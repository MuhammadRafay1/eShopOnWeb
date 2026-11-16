using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Typed HTTP client for the Upvest Investment API. Acquires and caches scoped OAuth2 tokens, signs every
/// request (including the token request) via <see cref="UpvestMessageSigner"/>, attaches idempotency keys to
/// writes, and maps wire JSON to the clean port models. Non-success responses become <see cref="UpvestException"/>.
/// Personal data is only ever sent to Upvest — never logged here.
/// </summary>
public class UpvestClient : IUpvestClient
{
    // Scopes per operation group.
    private const string ScopeUsers = "users:admin";
    private const string ScopeChecks = "checks:admin";
    private const string ScopeTaxes = "taxes:admin";
    private const string ScopeAccounts = "accounts:admin";
    private const string ScopePortfoliosAdmin = "portfolios:admin";
    private const string ScopePortfoliosRead = "portfolios:read";
    private const string ScopeTopupsAdmin = "topups:admin";
    private const string ScopeTopupsRead = "topups:read";
    private const string ScopeValuations = "valuations:read";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly UpvestMessageSigner _signer;
    private readonly UpvestSettings _settings;
    private readonly IAppLogger<UpvestClient> _logger;

    private static readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expiry)> TokenCache = new();

    public UpvestClient(HttpClient http, UpvestMessageSigner signer, IOptions<UpvestSettings> settings, IAppLogger<UpvestClient> logger)
    {
        _http = http;
        _signer = signer;
        _settings = settings.Value;
        _logger = logger;
        if (_http.BaseAddress is null && !string.IsNullOrEmpty(_settings.BaseUrl))
            _http.BaseAddress = new Uri(_settings.BaseUrl);
    }

    // ---------------- Flow 1: onboarding ----------------

    public async Task<UpvestResource> CreateUserAsync(InvestorSignup s, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["first_name"] = s.FirstName,
            ["last_name"] = s.LastName,
            ["email"] = s.Email,
            ["birth_date"] = s.BirthDate,
            ["birth_city"] = s.BirthCity,
            ["birth_country"] = s.BirthCountry,
            ["nationalities"] = new[] { s.Nationality },
            ["address"] = new Dictionary<string, object?>
            {
                ["address_line1"] = s.Address.Line1,
                ["postcode"] = s.Address.Postcode,
                ["city"] = s.Address.City,
                ["country"] = s.Address.Country
            },
            ["phone_number"] = SanitizePhone(s.PhoneNumber),
            ["fatca"] = new Dictionary<string, object?>
            {
                ["status"] = false,
                ["confirmed_at"] = Now()
            }
        };
        var (_, json) = await SendAsync(HttpMethod.Post, "/users", ScopeUsers, body, idempotent: true, ct);
        return ReadResource(json);
    }

    public async Task<UpvestResource?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        var (status, json) = await SendAsync(HttpMethod.Get, $"/users/{userId}", ScopeUsers, null, idempotent: false, ct, allowNotFound: true);
        return status == 404 ? null : ReadResource(json);
    }

    public async Task<UpvestResource> CreateUserIdentifierAsync(string userId, IdentityDocument doc, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = IsPassport(doc.Type) ? "PASSPORT" : "NATIONAL_ID",
            ["issuing_country"] = doc.IssuingCountry,
            ["identifier"] = doc.Number
        };
        var (_, json) = await SendAsync(HttpMethod.Post, $"/users/{userId}/identifiers", ScopeUsers, body, idempotent: true, ct);
        return ReadResource(json);
    }

    public async Task<string> CreateKycCheckAsync(string userId, InvestorSignup s, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "KYC",
            ["check_confirmed_at"] = Now(),
            ["data_download_link"] = "https://eshoponweb.example.com/kyc-evidence",
            ["document_type"] = IsPassport(s.IdentityDocument.Type) ? "PASSPORT" : "ID_CARD",
            ["document_expiration_date"] = s.IdentityDocument.ExpiryDate,
            ["nationality"] = s.Nationality,
            ["provider"] = "eShopOnWeb",
            ["method"] = "ELECTRONIC_ID",
            // Supplying a confirmed address satisfies proof-of-residence, so no separate POR check is needed.
            ["confirmed_address"] = new Dictionary<string, object?>
            {
                ["address_line1"] = s.Address.Line1,
                ["postcode"] = s.Address.Postcode,
                ["city"] = s.Address.City,
                ["country"] = s.Address.Country
            }
        };
        var (_, json) = await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", ScopeChecks, body, idempotent: true, ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task SetTaxResidenciesAsync(string userId, string taxCountry, string taxId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["tax_residencies"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["country"] = taxCountry,
                    ["tax_identifier_number"] = taxId
                }
            }
        };
        await SendAsync(HttpMethod.Post, $"/users/{userId}/tax_residencies", ScopeTaxes, body, idempotent: true, ct);
    }

    public async Task<UpvestResource> CreateAccountGroupAsync(string userId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["user_id"] = userId, ["type"] = "PERSONAL" };
        var (_, json) = await SendAsync(HttpMethod.Post, "/account_groups", ScopeAccounts, body, idempotent: true, ct);
        return ReadResource(json);
    }

    public async Task<UpvestResource> CreateAccountAsync(string userId, string accountGroupId, CancellationToken ct = default)
    {
        // A PORTFOLIO account holds a managed portfolio (orders are created on a portfolio basis).
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_group_id"] = accountGroupId,
            ["type"] = "PORTFOLIO"
        };
        var (_, json) = await SendAsync(HttpMethod.Post, "/accounts", ScopeAccounts, body, idempotent: true, ct);
        return ReadResource(json);
    }

    public async Task<UpvestResource?> GetAccountAsync(string accountId, CancellationToken ct = default)
    {
        var (status, json) = await SendAsync(HttpMethod.Get, $"/accounts/{accountId}", ScopeAccounts, null, idempotent: false, ct, allowNotFound: true);
        return status == 404 ? null : ReadResource(json);
    }

    // ---------------- Flow 2: model portfolios ----------------

    public async Task<UpvestAllocation> CreateAllocationAsync(string name, IEnumerable<ModelFund> funds, CancellationToken ct = default)
    {
        var (_, json) = await SendAsync(HttpMethod.Post, "/portfolios/allocations", ScopePortfoliosAdmin, AllocationBody(name, funds), idempotent: true, ct);
        return ReadAllocation(json);
    }

    public async Task<UpvestAllocation> UpdateAllocationAsync(string allocationId, string name, IEnumerable<ModelFund> funds, CancellationToken ct = default)
    {
        var (_, json) = await SendAsync(HttpMethod.Put, $"/portfolios/allocations/{allocationId}", ScopePortfoliosAdmin, AllocationBody(name, funds), idempotent: true, ct);
        return ReadAllocation(json);
    }

    public async Task<IReadOnlyList<UpvestAllocation>> ListAllocationsAsync(CancellationToken ct = default)
    {
        var (_, json) = await SendAsync(HttpMethod.Get, "/portfolios/allocations", ScopePortfoliosRead, null, idempotent: false, ct);
        using var doc = JsonDocument.Parse(json);
        var result = new List<UpvestAllocation>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var item in data.EnumerateArray())
                result.Add(ReadAllocation(item));
        return result;
    }

    public async Task SetConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default)
    {
        var createBody = new Dictionary<string, object?> { ["account_id"] = accountId, ["allocation_id"] = allocationId };
        var (status, _) = await SendAsync(HttpMethod.Post, "/portfolios/configurations", ScopePortfoliosAdmin, createBody, idempotent: true, ct, allowConflict: true);
        if (status == 409)
        {
            // A configuration already exists for this account — switch its allocation (PATCH).
            var patchBody = new Dictionary<string, object?> { ["allocation_id"] = allocationId };
            await SendAsync(HttpMethod.Patch, $"/portfolios/configurations/{accountId}", ScopePortfoliosAdmin, patchBody, idempotent: true, ct);
        }
    }

    // ---------------- Flow 3: paying in ----------------

    public async Task<UpvestResource> CreateTopupAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["account_group_id"] = accountGroupId,
            ["cash_amount"] = Money(amount),
            ["currency"] = currency
        };
        var (_, json) = await SendAsync(HttpMethod.Post, "/payments/topups", ScopeTopupsAdmin, body, idempotent: true, ct);
        return ReadResource(json);
    }

    public async Task<UpvestResource?> GetTopupAsync(string topupId, CancellationToken ct = default)
    {
        var (status, json) = await SendAsync(HttpMethod.Get, $"/payments/topups/{topupId}", ScopeTopupsRead, null, idempotent: false, ct, allowNotFound: true);
        return status == 404 ? null : ReadResource(json);
    }

    public async Task<UpvestResource> CreatePortfolioOrderAsync(string userId, string accountId, decimal amount, string currency, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_id"] = accountId,
            ["cash_amount"] = Money(amount),
            ["currency"] = currency,
            ["side"] = "BUY"
        };
        var (_, json) = await SendAsync(HttpMethod.Post, "/portfolios/orders", ScopePortfoliosAdmin, body, idempotent: true, ct);
        return ReadResource(json);
    }

    public async Task<UpvestResource?> GetPortfolioOrderAsync(string orderId, CancellationToken ct = default)
    {
        var (status, json) = await SendAsync(HttpMethod.Get, $"/portfolios/orders/{orderId}", ScopePortfoliosRead, null, idempotent: false, ct, allowNotFound: true);
        return status == 404 ? null : ReadResource(json);
    }

    // ---------------- Flow 4: realignment ----------------

    public async Task<string> TriggerRebalancingAsync(string allocationId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["allocations"] = new[] { allocationId } };
        var (_, json) = await SendAsync(HttpMethod.Post, "/portfolios/rebalancings/executions", ScopePortfoliosAdmin, body, idempotent: true, ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<IReadOnlyList<UpvestRebalancingOrder>> GetRebalancingOrdersAsync(string executionId, CancellationToken ct = default)
    {
        var (_, json) = await SendAsync(HttpMethod.Get, $"/portfolios/rebalancings/executions/{executionId}/orders", ScopePortfoliosRead, null, idempotent: false, ct);
        using var doc = JsonDocument.Parse(json);
        var result = new List<UpvestRebalancingOrder>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var item in data.EnumerateArray())
                result.Add(new UpvestRebalancingOrder(
                    item.GetProperty("id").GetString() ?? string.Empty,
                    item.TryGetProperty("status", out var st) ? st.GetString() ?? string.Empty : string.Empty));
        return result;
    }

    // ---------------- Flow 5: portfolio view ----------------

    public async Task<UpvestValuation?> GetCurrentValuationAsync(string accountId, CancellationToken ct = default)
    {
        var (status, json) = await SendAsync(HttpMethod.Get,
            $"/accounts/{accountId}/valuations/current?price_quality=HIGHEST_AVAILABLE",
            ScopeValuations, null, idempotent: false, ct, allowNotFound: true);
        if (status == 404) return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        decimal total = 0m;
        string currency = "EUR";
        if (root.TryGetProperty("total_security_value", out var tsv))
        {
            total = ReadMoney(tsv, "amount") ?? 0m;
            if (tsv.TryGetProperty("currency", out var cur)) currency = cur.GetString() ?? currency;
        }

        var positions = new List<UpvestPosition>();
        if (root.TryGetProperty("security_positions", out var sp) && sp.ValueKind == JsonValueKind.Array)
        {
            foreach (var pos in sp.EnumerateArray())
            {
                string instrumentId = string.Empty;
                if (pos.TryGetProperty("instrument", out var ins))
                    instrumentId = (ins.TryGetProperty("isin", out var isin) ? isin.GetString() : null)
                                   ?? (ins.TryGetProperty("uuid", out var uuid) ? uuid.GetString() : null)
                                   ?? string.Empty;

                decimal quantity = 0m;
                if (pos.TryGetProperty("quantity", out var q))
                    quantity = ParseDecimal(q) ?? 0m;

                decimal? value = null;
                string? posCurrency = null;
                if (pos.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.Object)
                {
                    value = ReadMoney(val, "amount");
                    if (val.TryGetProperty("currency", out var vc)) posCurrency = vc.GetString();
                }
                positions.Add(new UpvestPosition(instrumentId, quantity, value, posCurrency));
            }
        }

        return new UpvestValuation(total, currency, positions);
    }

    // ---------------- Core send / signing ----------------

    private async Task<(int Status, string Body)> SendAsync(
        HttpMethod method, string path, string scope, object? jsonBody, bool idempotent,
        CancellationToken ct, bool allowNotFound = false, bool allowConflict = false)
    {
        var token = await GetTokenAsync(scope, ct);

        using var request = new HttpRequestMessage(method, AbsoluteUri(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotent)
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        if (jsonBody is not null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(jsonBody, JsonOptions);
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
        }

        await _signer.SignAsync(request, ct);
        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        int status = (int)response.StatusCode;

        if (response.IsSuccessStatusCode) return (status, text);
        if (allowNotFound && status == 404) return (status, text);
        if (allowConflict && status == 409) return (status, text);

        throw BuildException(status, text);
    }

    private async Task<string> GetTokenAsync(string scope, CancellationToken ct)
    {
        if (TokenCache.TryGetValue(scope, out var cached) && cached.Expiry > DateTimeOffset.UtcNow)
            return cached.Token;

        string form = $"client_id={Uri.EscapeDataString(_settings.ClientId)}" +
                      $"&client_secret={Uri.EscapeDataString(_settings.ClientSecret)}" +
                      $"&grant_type=client_credentials&scope={Uri.EscapeDataString(scope)}";
        var bytes = Encoding.UTF8.GetBytes(form);

        using var request = new HttpRequestMessage(HttpMethod.Post, AbsoluteUri("/auth/token"));
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        request.Content = content;

        await _signer.SignAsync(request, ct);
        using var response = await _http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw BuildException((int)response.StatusCode, text);

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        string accessToken = root.GetProperty("access_token").GetString()!;
        int expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 1800;
        TokenCache[scope] = (accessToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60));
        return accessToken;
    }

    private Uri AbsoluteUri(string path)
    {
        var baseUrl = _settings.BaseUrl.TrimEnd('/');
        return new Uri(baseUrl + path);
    }

    private static UpvestException BuildException(int status, string body)
    {
        string detail = ExtractDetail(body);
        bool scopeFailure = status == 401 && detail.Contains("not authorised to use these scopes", StringComparison.OrdinalIgnoreCase);
        return new UpvestException(status, detail, scopeFailure);
    }

    private static string ExtractDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "(no response body)";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                return d.GetString()!;
            if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString()!;
        }
        catch (JsonException) { /* fall through */ }
        return body.Length > 500 ? body[..500] : body;
    }

    // ---------------- helpers ----------------

    private static Dictionary<string, object?> AllocationBody(string name, IEnumerable<ModelFund> funds) => new()
    {
        ["name"] = name,
        ["allocation"] = funds.Select(f => new Dictionary<string, object?>
        {
            ["instrument_id"] = f.InstrumentId,
            ["instrument_id_type"] = "ISIN",
            ["weight"] = Weight(f.Weight)
        }).ToArray()
    };

    private static UpvestResource ReadResource(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ReadResource(doc.RootElement);
    }

    private static UpvestResource ReadResource(JsonElement el)
    {
        string id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
        string? status = el.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null;
        return new UpvestResource(id, status);
    }

    private static UpvestAllocation ReadAllocation(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ReadAllocation(doc.RootElement);
    }

    private static UpvestAllocation ReadAllocation(JsonElement el)
    {
        string id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
        string? name = el.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var funds = new List<UpvestAllocationFund>();
        if (el.TryGetProperty("allocation", out var alloc) && alloc.ValueKind == JsonValueKind.Array)
            foreach (var f in alloc.EnumerateArray())
                funds.Add(new UpvestAllocationFund(
                    f.TryGetProperty("instrument_id", out var ii) ? ii.GetString() ?? string.Empty : string.Empty,
                    f.TryGetProperty("instrument_id_type", out var it) ? it.GetString() ?? "ISIN" : "ISIN",
                    f.TryGetProperty("weight", out var w) ? (w.ValueKind == JsonValueKind.String ? w.GetString()! : w.ToString()) : "0"));
        return new UpvestAllocation(id, name, funds);
    }

    private static decimal? ReadMoney(JsonElement parent, string prop)
        => parent.TryGetProperty(prop, out var el) ? ParseDecimal(el) : null;

    private static decimal? ParseDecimal(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var num)) return num;
        if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var str)) return str;
        return null;
    }

    // Upvest requires phone_number to be 8–15 digits only; drop any formatting and omit if it doesn't fit.
    private static string? SanitizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length is >= 8 and <= 15 ? digits : null;
    }

    private static bool IsPassport(string? type) =>
        !string.IsNullOrEmpty(type) && type.Contains("passport", StringComparison.OrdinalIgnoreCase);

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Weight(decimal weight) => weight.ToString("0.#####", CultureInfo.InvariantCulture);
    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
}
