using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Portfolios;

namespace Microsoft.eShopWeb.Infrastructure.Portfolios;

/// <summary>
/// Maps this integration's clean Upvest models onto the Upvest Investment API's requests and responses.
/// Each method requests a token carrying the least-privilege scope the operation needs, signs the call,
/// and translates Upvest errors into <see cref="UpvestApiException"/>. No personal data is ever logged.
/// </summary>
public sealed class UpvestInvestmentClient : IUpvestInvestmentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly UpvestHttpClient _http;
    private readonly UpvestTokenService _tokens;

    public UpvestInvestmentClient(UpvestHttpClient http, UpvestTokenService tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    // ------------------------------------------------------------------ onboarding

    public async Task<UpvestUser> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        string now = Iso(DateTimeOffset.UtcNow);
        var body = new Dictionary<string, object?>
        {
            ["first_name"] = request.FirstName,
            ["last_name"] = request.LastName,
            ["email"] = request.Email,
            ["birth_date"] = request.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["birth_city"] = NullIfBlank(request.BirthCity),
            ["birth_country"] = NullIfBlank(request.BirthCountry),
            ["nationalities"] = new[] { request.Nationality },
            ["phone_number"] = DigitsOrNull(request.PhoneNumber),
            ["address"] = new Dictionary<string, object?>
            {
                ["address_line1"] = request.Address.AddressLine1,
                ["postcode"] = request.Address.Postcode,
                ["city"] = request.Address.City,
                ["country"] = request.Address.Country
            },
            // FATCA is required. Terms/data-privacy consents are optional and tenant-specific, so we
            // omit them rather than send placeholder consent-document ids the tenant would reject.
            ["fatca"] = new Dictionary<string, object?> { ["status"] = false, ["confirmed_at"] = now }
        };

        var doc = await PostAsync("/users", body, UpvestScopes.UsersAdmin, "create user", ct);
        var root = doc.RootElement;
        return new UpvestUser(GetString(root, "id"), GetString(root, "status"));
    }

    public async Task<UpvestUser> GetUserAsync(string userId, CancellationToken ct = default)
    {
        // GET /users accepts users:read OR users:admin; this client is granted users:admin.
        var doc = await GetAsync($"/users/{Escape(userId)}", UpvestScopes.UsersAdmin, "get user", ct);
        var root = doc.RootElement;
        return new UpvestUser(GetString(root, "id"), GetString(root, "status"));
    }

    public async Task<string> SubmitKycCheckAsync(string userId, UpvestIdentityDocument document, string nationality, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "KYC",
            ["check_confirmed_at"] = Iso(DateTimeOffset.UtcNow),
            ["data_download_link"] = "https://eshoponweb.example.com/kyc/evidence.zip",
            ["document_type"] = MapDocumentType(document.Type),
            ["document_expiration_date"] = document.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["nationality"] = nationality,
            ["provider"] = "eShopOnWeb Identity",
            ["method"] = "ELECTRONIC_ID"
        };

        var doc = await PostAsync($"/users/{Escape(userId)}/checks", body, UpvestScopes.ChecksAdmin, "submit KYC check", ct);
        return GetString(doc.RootElement, "id");
    }

    public async Task SetTaxResidencyAsync(string userId, string taxCountry, string taxId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["tax_residencies"] = new[]
            {
                new Dictionary<string, object?> { ["country"] = taxCountry, ["tax_identifier_number"] = taxId }
            }
        };
        await PostAsync($"/users/{Escape(userId)}/tax_residencies", body, UpvestScopes.TaxesAdmin, "set tax residency", ct);
    }

    public async Task CreatePassportIdentifierAsync(string userId, string issuingCountry, string number, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "PASSPORT",
            ["issuing_country"] = issuingCountry,
            ["identifier"] = number,
            ["identifier_standard"] = "GPN"
        };
        await PostAsync($"/users/{Escape(userId)}/identifiers", body, UpvestScopes.UsersAdmin, "create identifier", ct);
    }

    public async Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["user_id"] = userId, ["type"] = "PERSONAL" };
        var doc = await PostAsync("/account_groups", body, UpvestScopes.AccountsAdmin, "create account group", ct);
        var root = doc.RootElement;
        return new UpvestAccountGroup(GetString(root, "id"), GetString(root, "status"));
    }

    public async Task<UpvestAccount> CreatePortfolioAccountAsync(string userId, string accountGroupId, string name, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_group_id"] = accountGroupId,
            ["type"] = "PORTFOLIO",
            ["name"] = name
        };
        var doc = await PostAsync("/accounts", body, UpvestScopes.AccountsAdmin, "create account", ct);
        var root = doc.RootElement;
        return new UpvestAccount(GetString(root, "id"), GetString(root, "status"));
    }

    public async Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken ct = default)
    {
        var doc = await GetAsync($"/accounts/{Escape(accountId)}", UpvestScopes.AccountsRead, "get account", ct);
        var root = doc.RootElement;
        return new UpvestAccount(GetString(root, "id"), GetString(root, "status"));
    }

    // ------------------------------------------------------------------ allocations

    public async Task<UpvestAllocation> CreateAllocationAsync(string name, IReadOnlyList<UpvestAllocationFund> funds, CancellationToken ct = default)
    {
        var body = AllocationBody(name, funds);
        var doc = await PostAsync("/portfolios/allocations", body, UpvestScopes.PortfoliosAdmin, "create allocation", ct);
        return ParseAllocation(doc.RootElement);
    }

    public async Task<UpvestAllocation> ReplaceAllocationAsync(string allocationId, string name, IReadOnlyList<UpvestAllocationFund> funds, CancellationToken ct = default)
    {
        var body = AllocationBody(name, funds);
        var doc = await SendBodyAsync(HttpMethod.Put, $"/portfolios/allocations/{Escape(allocationId)}", body, UpvestScopes.PortfoliosAdmin, "replace allocation", ct);
        return ParseAllocation(doc.RootElement);
    }

    public async Task SetPortfolioConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default)
    {
        // Create the configuration; if it already exists, re-link via PATCH.
        var createBody = new Dictionary<string, object?> { ["account_id"] = accountId, ["allocation_id"] = allocationId };
        try
        {
            await PostAsync("/portfolios/configurations", createBody, UpvestScopes.PortfoliosAdmin, "create portfolio configuration", ct);
        }
        catch (UpvestApiException ex) when (ex.StatusCode is 409 or 422)
        {
            var patchBody = new Dictionary<string, object?> { ["allocation_id"] = allocationId };
            await SendBodyAsync(HttpMethod.Patch, $"/portfolios/configurations/{Escape(accountId)}", patchBody, UpvestScopes.PortfoliosAdmin, "update portfolio configuration", ct);
        }
    }

    // ------------------------------------------------------------------ paying in

    public async Task<UpvestTopup> CreateTopupAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["account_group_id"] = accountGroupId,
            ["cash_amount"] = Money(amount),
            ["currency"] = currency
        };
        var doc = await PostAsync("/payments/topups", body, UpvestScopes.TopupsAdmin, "create top-up", ct);
        var root = doc.RootElement;
        return new UpvestTopup(GetString(root, "id"), TryGetString(root, "status") ?? "CONFIRMED");
    }

    public async Task<UpvestPortfolioOrder> CreateBuyOrderAsync(string userId, string accountId, decimal amount, string currency, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["account_id"] = accountId,
            ["side"] = "BUY",
            ["cash_amount"] = Money(amount),
            ["currency"] = currency
        };
        var doc = await PostAsync("/portfolios/orders", body, UpvestScopes.PortfoliosAdmin, "create portfolio order", ct);
        var root = doc.RootElement;
        return new UpvestPortfolioOrder(GetString(root, "id"), GetString(root, "status"));
    }

    public async Task<UpvestPortfolioOrder> GetPortfolioOrderAsync(string portfolioOrderId, CancellationToken ct = default)
    {
        var doc = await GetAsync($"/portfolios/orders/{Escape(portfolioOrderId)}", UpvestScopes.PortfoliosRead, "get portfolio order", ct);
        var root = doc.RootElement;
        return new UpvestPortfolioOrder(GetString(root, "id"), GetString(root, "status"));
    }

    // ------------------------------------------------------------------ realignment

    public async Task<UpvestRebalancingExecution> CreateRebalancingForAllocationAsync(string allocationId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["allocations"] = new[] { allocationId } };
        var doc = await PostAsync("/portfolios/rebalancings/executions", body, UpvestScopes.PortfoliosAdmin, "start realignment", ct);
        return new UpvestRebalancingExecution(GetString(doc.RootElement, "id"));
    }

    public async Task<IReadOnlyList<string>> GetRebalancingOrderStatusesAsync(string executionId, CancellationToken ct = default)
    {
        var doc = await GetAsync($"/portfolios/rebalancings/executions/{Escape(executionId)}/orders", UpvestScopes.PortfoliosRead, "read realignment orders", ct);
        var statuses = new List<string>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var order in data.EnumerateArray())
            {
                var status = TryGetString(order, "status");
                if (status is not null) statuses.Add(status);
            }
        }
        return statuses;
    }

    // ------------------------------------------------------------------ valuation

    public async Task<UpvestValuation> GetCurrentValuationAsync(string accountId, CancellationToken ct = default)
    {
        var doc = await GetAsync($"/accounts/{Escape(accountId)}/valuations/current", UpvestScopes.ValuationsRead, "get valuation", ct);
        var root = doc.RootElement;

        decimal totalValue = 0m;
        string currency = "EUR";
        if (root.TryGetProperty("total_security_value", out var total))
        {
            totalValue = ParseMoney(TryGetString(total, "amount"));
            currency = TryGetString(total, "currency") ?? currency;
        }

        var holdings = new List<UpvestHolding>();
        if (root.TryGetProperty("security_positions", out var positions) && positions.ValueKind == JsonValueKind.Array)
        {
            foreach (var position in positions.EnumerateArray())
            {
                string isin = "";
                if (position.TryGetProperty("instrument", out var instrument))
                    isin = TryGetString(instrument, "isin") ?? TryGetString(instrument, "uuid") ?? "";

                decimal quantity = ParseMoney(TryGetString(position, "quantity"));

                decimal value = 0m;
                if (position.TryGetProperty("value", out var valueObj) && valueObj.ValueKind == JsonValueKind.Object)
                    value = ParseMoney(TryGetString(valueObj, "amount"));

                holdings.Add(new UpvestHolding(isin, quantity, value));
            }
        }

        return new UpvestValuation(holdings, totalValue, currency);
    }

    // ------------------------------------------------------------------ transport helpers

    private Task<JsonDocument> PostAsync(string path, object body, string scope, string operation, CancellationToken ct)
        => SendBodyAsync(HttpMethod.Post, path, body, scope, operation, ct);

    private async Task<JsonDocument> SendBodyAsync(HttpMethod method, string path, object body, string scope, string operation, CancellationToken ct)
    {
        string token = await _tokens.GetTokenAsync(scope, ct);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        bool withIdempotency = method == HttpMethod.Post;
        var response = await _http.SendAsync(method, path, query: null, body: payload,
            contentType: "application/json", bearerToken: token, withIdempotencyKey: withIdempotency, ct);

        if (!response.IsSuccess)
            throw new UpvestApiException(response.Status, UpvestErrors.Describe(response, operation));

        return ParseOrEmpty(response.Body);
    }

    private async Task<JsonDocument> GetAsync(string path, string scope, string operation, CancellationToken ct)
    {
        string token = await _tokens.GetTokenAsync(scope, ct);
        var response = await _http.SendAsync(HttpMethod.Get, path, query: null, body: null,
            contentType: null, bearerToken: token, withIdempotencyKey: false, ct);

        if (!response.IsSuccess)
            throw new UpvestApiException(response.Status, UpvestErrors.Describe(response, operation));

        return ParseOrEmpty(response.Body);
    }

    private static Dictionary<string, object?> AllocationBody(string name, IReadOnlyList<UpvestAllocationFund> funds) => new()
    {
        ["name"] = name,
        ["allocation"] = funds.Select(f => new Dictionary<string, object?>
        {
            ["instrument_id"] = f.Isin,
            ["instrument_id_type"] = "ISIN",
            ["weight"] = f.Weight.ToString(CultureInfo.InvariantCulture)
        }).ToArray()
    };

    private static UpvestAllocation ParseAllocation(JsonElement root)
    {
        var funds = new List<UpvestAllocationFund>();
        if (root.TryGetProperty("allocation", out var allocation) && allocation.ValueKind == JsonValueKind.Array)
        {
            foreach (var fund in allocation.EnumerateArray())
            {
                funds.Add(new UpvestAllocationFund(
                    TryGetString(fund, "instrument_id") ?? "",
                    ParseMoney(TryGetString(fund, "weight"))));
            }
        }
        return new UpvestAllocation(GetString(root, "id"), TryGetString(root, "name") ?? "", funds);
    }

    private static string MapDocumentType(string type) => (type ?? "").Trim().ToUpperInvariant() switch
    {
        "PASSPORT" => "PASSPORT",
        "ID_CARD" or "IDCARD" or "ID" or "NATIONAL_ID" or "IDENTITY_CARD" => "ID_CARD",
        "RESIDENCE_PERMIT" or "RESIDENCEPERMIT" => "RESIDENCE_PERMIT",
        "DRIVING_LICENSE" or "DRIVING_LICENCE" or "DRIVERS_LICENSE" or "DRIVER_LICENSE" => "DRIVING_LICENSE",
        _ => "PASSPORT"
    };

    private static string Money(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private static decimal ParseMoney(string? raw) =>
        decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static string Iso(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string Escape(string segment) => Uri.EscapeDataString(segment);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Upvest requires phone numbers as 8–15 digits with no separators or leading '+'.</summary>
    private static string? DigitsOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static JsonDocument ParseOrEmpty(string body)
        => string.IsNullOrWhiteSpace(body) ? JsonDocument.Parse("{}") : JsonDocument.Parse(body);

    private static string GetString(JsonElement element, string property)
        => TryGetString(element, property) ?? throw new UpvestApiException(502, $"Upvest response was missing '{property}'.");

    private static string? TryGetString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
