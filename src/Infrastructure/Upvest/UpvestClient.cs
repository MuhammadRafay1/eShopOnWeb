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
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// HTTP implementation of <see cref="IUpvestClient"/>. Handles scoped token acquisition,
/// HTTP message signing, idempotency keys, transient-failure retries and problem+json error
/// mapping for every Upvest call. Request bodies use Upvest's exact snake_case field names.
/// </summary>
public sealed class UpvestClient : IUpvestClient
{
    private static readonly HashSet<string> ConcatNationalities =
        new(StringComparer.OrdinalIgnoreCase) { "AT", "DE", "FR", "HU", "IE", "LU" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly UpvestMessageSigner _signer;
    private readonly UpvestTokenProvider _tokenProvider;

    public UpvestClient(HttpClient httpClient, UpvestMessageSigner signer, UpvestTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _signer = signer;
        _tokenProvider = tokenProvider;
    }

    // ---------------- Onboarding ----------------

    public async Task<UpvestUser> CreateUserAsync(InvestorSignUp s, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["first_name"] = s.FirstName,
            ["last_name"] = s.LastName,
            ["email"] = s.Email,
            ["birth_date"] = s.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["birth_city"] = string.IsNullOrWhiteSpace(s.BirthCity) ? null : s.BirthCity,
            ["birth_country"] = string.IsNullOrWhiteSpace(s.BirthCountry) ? null : s.BirthCountry,
            ["nationalities"] = new[] { s.Nationality },
            ["phone_number"] = NormalizePhone(s.PhoneNumber),
            ["address"] = AddressBody(s.Address),
            ["fatca"] = new Dictionary<string, object?>
            {
                ["status"] = false,
                ["confirmed_at"] = UtcNow()
            }
        };
        using var doc = await SendAsync(HttpMethod.Post, "/users", null, "users:admin", body, ct).ConfigureAwait(false);
        var root = doc!.RootElement;
        return new UpvestUser(GetString(root, "id")!, GetString(root, "status") ?? "INACTIVE");
    }

    public async Task<UpvestUser?> GetUserAsync(string userId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/users/{userId}", null, "users:read", null, ct, allowNotFound: true).ConfigureAwait(false);
        if (doc is null) return null;
        var root = doc.RootElement;
        return new UpvestUser(GetString(root, "id")!, GetString(root, "status") ?? "INACTIVE");
    }

    public async Task SubmitKycCheckAsync(string userId, KycCheckInput check, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "KYC",
            ["check_confirmed_at"] = UtcNow(),
            ["data_download_link"] = $"https://verification.eshoponweb.local/kyc/{userId}.zip",
            ["document_type"] = check.DocumentType,
            ["document_expiration_date"] = check.DocumentExpirationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["nationality"] = check.Nationality,
            ["provider"] = "eShopOnWeb",
            ["method"] = "ELECTRONIC_ID",
            ["confirmed_address"] = AddressBody(check.ConfirmedAddress)
        };
        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", null, "checks:admin", body, ct).ConfigureAwait(false);
    }

    public async Task SubmitInstrumentFitCheckAsync(string userId, bool suitable, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "INSTRUMENT_FIT",
            ["check_confirmed_at"] = UtcNow(),
            ["instrument_suitability"] = new Dictionary<string, object?> { ["suitability"] = suitable }
        };
        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", null, "checks:admin", body, ct).ConfigureAwait(false);
    }

    public async Task CreateIdentifierAsync(string userId, string type, string issuingCountry, string identifierStandard, string identifier, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["issuing_country"] = issuingCountry,
            ["identifier_standard"] = identifierStandard,
            ["identifier"] = identifier
        };
        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/identifiers", null, "users:admin", body, ct).ConfigureAwait(false);
    }

    public async Task SetTaxResidenciesAsync(string userId, IReadOnlyList<TaxResidencyInput> residencies, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["tax_residencies"] = residencies.Select(r => new Dictionary<string, object?>
            {
                ["country"] = r.Country,
                ["tax_identifier_number"] = string.IsNullOrWhiteSpace(r.TaxIdentifierNumber) ? null : r.TaxIdentifierNumber
            }).ToArray()
        };
        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/tax_residencies", null, "taxes:admin", body, ct).ConfigureAwait(false);
    }

    public async Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, string type, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["user_id"] = userId, ["type"] = type };
        using var doc = await SendAsync(HttpMethod.Post, "/account_groups", null, "accounts:admin", body, ct).ConfigureAwait(false);
        var root = doc!.RootElement;
        return new UpvestAccountGroup(GetString(root, "id")!, GetString(root, "status") ?? "PENDING_APPROVAL");
    }

    public async Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, string type, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_group_id"] = accountGroupId,
            ["type"] = type,
            ["name"] = "Managed Portfolio"
        };
        using var doc = await SendAsync(HttpMethod.Post, "/accounts", null, "accounts:admin", body, ct).ConfigureAwait(false);
        var root = doc!.RootElement;
        return new UpvestAccount(GetString(root, "id")!, GetString(root, "status") ?? "PENDING_APPROVAL");
    }

    public async Task<UpvestAccount?> GetAccountAsync(string accountId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/accounts/{accountId}", null, "accounts:read", null, ct, allowNotFound: true).ConfigureAwait(false);
        if (doc is null) return null;
        var root = doc.RootElement;
        return new UpvestAccount(GetString(root, "id")!, GetString(root, "status") ?? "PENDING_APPROVAL");
    }

    // ---------------- Model portfolios (allocations) ----------------

    public Task<UpvestAllocation> CreateAllocationAsync(string name, IReadOnlyList<UpvestFund> funds, CancellationToken ct = default)
        => UpsertAllocationAsync(HttpMethod.Post, "/portfolios/allocations", name, funds, ct);

    public Task<UpvestAllocation> ReplaceAllocationAsync(string allocationId, string name, IReadOnlyList<UpvestFund> funds, CancellationToken ct = default)
        => UpsertAllocationAsync(HttpMethod.Put, $"/portfolios/allocations/{allocationId}", name, funds, ct);

    private async Task<UpvestAllocation> UpsertAllocationAsync(HttpMethod method, string path, string name, IReadOnlyList<UpvestFund> funds, CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["allocation"] = funds.Select(f => new Dictionary<string, object?>
            {
                ["instrument_id"] = f.InstrumentId,
                ["instrument_id_type"] = f.InstrumentIdType,
                ["weight"] = f.Weight
            }).ToArray()
        };
        using var doc = await SendAsync(method, path, null, "portfolios:admin", body, ct).ConfigureAwait(false);
        return ParseAllocation(doc!.RootElement);
    }

    public async Task<IReadOnlyList<UpvestAllocation>> ListAllocationsAsync(CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/portfolios/allocations", null, "portfolios:read", null, ct).ConfigureAwait(false);
        var result = new List<UpvestAllocation>();
        if (doc!.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var item in data.EnumerateArray())
                result.Add(ParseAllocation(item));
        return result;
    }

    // ---------------- Portfolio configuration ----------------

    public async Task CreateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["account_id"] = accountId, ["allocation_id"] = allocationId };
        using var _ = await SendAsync(HttpMethod.Post, "/portfolios/configurations", null, "portfolios:admin", body, ct).ConfigureAwait(false);
    }

    public async Task UpdateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["allocation_id"] = allocationId };
        using var _ = await SendAsync(HttpMethod.Patch, $"/portfolios/configurations/{accountId}", null, "portfolios:admin", body, ct).ConfigureAwait(false);
    }

    public async Task<UpvestConfiguration?> GetConfigurationAsync(string accountId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/portfolios/configurations/{accountId}", null, "portfolios:read", null, ct, allowNotFound: true).ConfigureAwait(false);
        if (doc is null) return null;
        var root = doc.RootElement;
        return new UpvestConfiguration(GetString(root, "account_id") ?? accountId, GetString(root, "allocation_id") ?? string.Empty);
    }

    // ---------------- Paying in ----------------

    public async Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["account_group_id"] = accountGroupId,
            ["amount"] = FormatAmount(amount),
            ["currency"] = currency
        };
        using var _ = await SendAsync(HttpMethod.Post, "/virtual_cash_balances/increases", null, "virtual_cash_balances:admin", body, ct).ConfigureAwait(false);
    }

    public async Task<UpvestPortfolioOrder> CreatePortfolioOrderAsync(string userId, string accountId, decimal cashAmount, string currency, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_id"] = accountId,
            ["side"] = "BUY",
            ["cash_amount"] = FormatAmount(cashAmount),
            ["currency"] = currency
        };
        using var doc = await SendAsync(HttpMethod.Post, "/portfolios/orders", null, "portfolios:admin", body, ct).ConfigureAwait(false);
        var root = doc!.RootElement;
        return new UpvestPortfolioOrder(GetString(root, "id")!, GetString(root, "status") ?? "NEW");
    }

    public async Task<UpvestPortfolioOrder?> GetPortfolioOrderAsync(string portfolioOrderId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/portfolios/orders/{portfolioOrderId}", null, "portfolios:read", null, ct, allowNotFound: true).ConfigureAwait(false);
        if (doc is null) return null;
        var root = doc.RootElement;
        return new UpvestPortfolioOrder(GetString(root, "id")!, GetString(root, "status") ?? "NEW");
    }

    // ---------------- Realignment (rebalancing) ----------------

    public async Task<string> TriggerRealignmentAsync(string allocationId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["allocations"] = new[] { allocationId } };
        using var doc = await SendAsync(HttpMethod.Post, "/portfolios/rebalancings/executions", null, "portfolios:admin", body, ct).ConfigureAwait(false);
        return GetString(doc!.RootElement, "id")!;
    }

    public async Task<bool> RealignmentExistsAsync(string executionId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/portfolios/rebalancings/executions/{executionId}", null, "portfolios:read", null, ct, allowNotFound: true).ConfigureAwait(false);
        return doc is not null;
    }

    public async Task<IReadOnlyList<UpvestRebalancingOrder>> ListRealignmentOrdersAsync(string executionId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/portfolios/rebalancings/executions/{executionId}/orders", null, "portfolios:read", null, ct).ConfigureAwait(false);
        var result = new List<UpvestRebalancingOrder>();
        if (doc!.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var item in data.EnumerateArray())
                result.Add(new UpvestRebalancingOrder(
                    GetString(item, "id") ?? string.Empty,
                    GetString(item, "status") ?? "NEW",
                    GetString(item, "cancellation_reason")));
        return result;
    }

    // ---------------- Portfolio valuation ----------------

    public async Task<UpvestValuation?> GetCurrentValuationAsync(string accountId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/accounts/{accountId}/valuations/current", null, "valuations:read", null, ct, allowNotFound: true).ConfigureAwait(false);
        if (doc is null) return null;
        var root = doc.RootElement;

        decimal total = 0m;
        string currency = "EUR";
        if (root.TryGetProperty("total_security_value", out var tsv))
        {
            total = ParseDecimal(GetString(tsv, "amount"));
            currency = GetString(tsv, "currency") ?? currency;
        }

        var positions = new List<UpvestPosition>();
        if (root.TryGetProperty("security_positions", out var secPos) && secPos.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in secPos.EnumerateArray())
            {
                string instrumentId = "unknown";
                if (p.TryGetProperty("instrument", out var inst))
                    instrumentId = GetString(inst, "isin") ?? GetString(inst, "uuid") ?? GetString(inst, "id") ?? instrumentId;
                decimal quantity = ParseDecimal(GetString(p, "quantity"));
                decimal value = p.TryGetProperty("value", out var v) ? ParseDecimal(GetString(v, "amount")) : 0m;
                positions.Add(new UpvestPosition(instrumentId, quantity, value));
            }
        }
        return new UpvestValuation(total, currency, positions);
    }

    // ---------------- Core request pipeline ----------------

    private async Task<JsonDocument?> SendAsync(
        HttpMethod method, string path, string? query, string scope,
        object? body, CancellationToken ct, bool allowNotFound = false)
    {
        if (_httpClient.BaseAddress is null)
            throw new UpvestApiException(500, null, "Upvest base URL is not configured (Upvest:BaseUrl).");

        byte[]? bodyBytes = null;
        if (body is not null)
            bodyBytes = JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);

        // Idempotency key is stable across retries of the same logical request.
        string? idempotencyKey = (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch)
            ? Guid.NewGuid().ToString()
            : null;

        const int maxAttempts = 3;
        bool reauthorised = false;
        UpvestApiException? lastError = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            string token = await _tokenProvider.GetTokenAsync(scope, ct).ConfigureAwait(false);

            using var request = new HttpRequestMessage(method, new Uri(_httpClient.BaseAddress!, path + (query ?? string.Empty)));
            if (bodyBytes is not null)
            {
                request.Content = new ByteArrayContent(bodyBytes);
                request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            }
            _signer.Sign(request, bodyBytes, bodyBytes is not null ? "application/json" : null, token, idempotencyKey);

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string ok = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(ok) ? JsonDocument.Parse("{}") : JsonDocument.Parse(ok);
            }

            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                return null;

            string errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            lastError = UpvestErrorMapper.ToException((int)response.StatusCode, errorBody);

            // Expired token (but not a scope-authorisation failure): refresh once and retry.
            if (response.StatusCode == HttpStatusCode.Unauthorized && !lastError.IsNotAuthorisedScopes && !reauthorised)
            {
                reauthorised = true;
                continue;
            }

            // Retry transient failures with a short backoff.
            int code = (int)response.StatusCode;
            if ((code == 429 || code == 500 || code == 503) && attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), ct).ConfigureAwait(false);
                continue;
            }

            throw lastError;
        }

        throw lastError ?? new UpvestApiException(502, null, "Upvest request failed.");
    }

    // ---------------- Helpers ----------------

    public static bool RequiresIdentifier(string nationality) => !ConcatNationalities.Contains(nationality);

    private static Dictionary<string, object?> AddressBody(PostalAddress a) => new()
    {
        ["address_line1"] = a.Line1,
        ["postcode"] = a.Postcode,
        ["city"] = a.City,
        ["country"] = a.Country
    };

    private static UpvestAllocation ParseAllocation(JsonElement root)
    {
        var funds = new List<UpvestFund>();
        if (root.TryGetProperty("allocation", out var alloc) && alloc.ValueKind == JsonValueKind.Array)
            foreach (var f in alloc.EnumerateArray())
                funds.Add(new UpvestFund(
                    GetString(f, "instrument_id") ?? string.Empty,
                    GetString(f, "instrument_id_type") ?? "ISIN",
                    GetString(f, "weight") ?? "0"));
        return new UpvestAllocation(GetString(root, "id") ?? string.Empty, GetString(root, "name") ?? string.Empty, funds);
    }

    private static string? GetString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Number
                ? n.ToString()
                : null);

    private static decimal ParseDecimal(string? s)
        => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static string FormatAmount(decimal amount)
        => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string UtcNow()
        => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>Reduces a phone number to the digits Upvest accepts (8–15 digits), or null.</summary>
    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length is >= 8 and <= 15 ? digits : null;
    }
}
