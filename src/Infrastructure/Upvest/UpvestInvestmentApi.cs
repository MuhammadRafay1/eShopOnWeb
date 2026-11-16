using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Talks to the Upvest Investment API over a signed, bearer-authenticated HttpClient. Each method
/// requests the minimal OAuth scope it needs so that operations the sandbox client is authorised
/// for succeed independently of those it is not. Personal data is sent to Upvest but never logged.
/// </summary>
public sealed class UpvestInvestmentApi : IUpvestInvestmentApi
{
    public const string HttpClientName = "upvest-api";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public UpvestInvestmentApi(HttpClient client) => _client = client;

    // ------------------------------------------------------------- Flow 1

    public async Task<UpvestUser> CreateUserAsync(InvestorSignUp s, CancellationToken ct = default)
    {
        var address = new
        {
            address_line1 = s.Address.Line1,
            postcode = s.Address.Postcode,
            city = s.Address.City,
            country = s.Address.Country
        };

        var body = new
        {
            first_name = s.FirstName,
            last_name = s.LastName,
            email = s.Email,
            birth_date = s.BirthDate.ToString("yyyy-MM-dd"),
            birth_city = s.BirthCity,
            birth_country = s.BirthCountry,
            nationalities = new[] { s.Nationality },
            address,
            postal_address = address
        };

        using var doc = await SendAsync(HttpMethod.Post, "/users", "users:admin", body, ct);
        var root = doc!.RootElement;
        return new UpvestUser(root.GetProperty("id").GetString()!, GetStringOrDefault(root, "status", "PENDING"));
    }

    public async Task SubmitKycCheckAsync(string userId, InvestorSignUp s, CancellationToken ct = default)
    {
        var body = new
        {
            type = "KYC",
            check_confirmed_at = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            data_download_link = "https://eshop.example.com/kyc/placeholder.zip",
            document_type = MapDocumentType(s.IdentityDocument.Type),
            document_expiration_date = s.IdentityDocument.ExpiryDate.ToString("yyyy-MM-dd"),
            nationality = s.IdentityDocument.IssuingCountry,
            provider = "eShopOnWeb",
            method = "VIDEO_ID"
        };

        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", "checks:admin", body, ct);
    }

    public async Task SetTaxResidencyAsync(string userId, string taxCountry, string taxId, CancellationToken ct = default)
    {
        var body = new
        {
            tax_residencies = new[] { new { country = taxCountry, tax_identifier_number = taxId } }
        };

        using var _ = await SendAsync(HttpMethod.Post, $"/users/{userId}/tax_residencies", "taxes:admin", body, ct);
    }

    public async Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken ct = default)
    {
        var body = new { user_id = userId, type = "PERSONAL" };
        using var doc = await SendAsync(HttpMethod.Post, "/account_groups", "accounts:admin", body, ct);
        var root = doc!.RootElement;
        return new UpvestAccountGroup(root.GetProperty("id").GetString()!, GetStringOrDefault(root, "status", "PENDING"));
    }

    public async Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, CancellationToken ct = default)
    {
        var body = new { user_id = userId, account_group_id = accountGroupId, type = "TRADING", name = "Managed portfolio" };
        using var doc = await SendAsync(HttpMethod.Post, "/accounts", "accounts:admin", body, ct);
        var root = doc!.RootElement;
        return new UpvestAccount(root.GetProperty("id").GetString()!, GetStringOrDefault(root, "status", "PENDING"));
    }

    public async Task<UpvestUser> GetUserAsync(string userId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/users/{userId}", "users:read", null, ct);
        var root = doc!.RootElement;
        return new UpvestUser(root.GetProperty("id").GetString()!, GetStringOrDefault(root, "status", "PENDING"));
    }

    public async Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/accounts/{accountId}", "accounts:read", null, ct);
        var root = doc!.RootElement;
        return new UpvestAccount(root.GetProperty("id").GetString()!, GetStringOrDefault(root, "status", "PENDING"));
    }

    // ------------------------------------------------------------- Flow 2

    public async Task<string> CreateAllocationAsync(string name, IReadOnlyList<FundWeightInput> funds, CancellationToken ct = default)
    {
        var body = new { name, allocation = ToAllocation(funds) };
        using var doc = await SendAsync(HttpMethod.Post, "/portfolios/allocations", "portfolios:admin", body, ct);
        return doc!.RootElement.GetProperty("id").GetString()!;
    }

    public async Task ReplaceAllocationAsync(string allocationId, string name, IReadOnlyList<FundWeightInput> funds, CancellationToken ct = default)
    {
        var body = new { allocation = ToAllocation(funds) };
        using var _ = await SendAsync(HttpMethod.Put, $"/portfolios/allocations/{allocationId}", "portfolios:admin", body, ct);
    }

    public async Task CreateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default)
    {
        var body = new { account_id = accountId, allocation_id = allocationId };
        using var _ = await SendAsync(HttpMethod.Post, "/portfolios/configurations", "portfolios:admin", body, ct);
    }

    public async Task UpdateConfigurationAsync(string accountId, string allocationId, CancellationToken ct = default)
    {
        var body = new { allocation_id = allocationId };
        using var _ = await SendAsync(HttpMethod.Patch, $"/portfolios/configurations/{accountId}", "portfolios:admin", body, ct);
    }

    // ------------------------------------------------------------- Flow 3

    public async Task FundAccountAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct = default)
    {
        // Sandbox funding: obtain a virtual bank account for the group, then simulate an inbound transfer to it.
        var vbaBody = new { account_group_id = accountGroupId, name = "Managed portfolio funding" };
        using var vbaDoc = await SendAsync(HttpMethod.Post, "/payments/virtual_bank_accounts", "payments:admin", vbaBody, ct);

        var iban = vbaDoc!.RootElement
            .GetProperty("identification").GetProperty("swift").GetProperty("iban").GetString()!;

        var txBody = new
        {
            account = new { iban },
            amount = FormatAmount(amount),
            currency,
            remittance_information = "Managed portfolio pay-in"
        };

        using var _ = await SendAsync(HttpMethod.Post, "/tests/payments/transactions", "tests:admin", txBody, ct);
    }

    public async Task<UpvestPortfolioOrder> CreatePortfolioOrderAsync(string userId, string accountId, decimal amount, string currency, CancellationToken ct = default)
    {
        var body = new
        {
            user_id = userId,
            account_id = accountId,
            cash_amount = FormatAmount(amount),
            currency,
            side = "BUY"
        };

        using var doc = await SendAsync(HttpMethod.Post, "/portfolios/orders", "portfolios:admin", body, ct);
        var root = doc!.RootElement;
        return new UpvestPortfolioOrder(root.GetProperty("id").GetString()!, GetStringOrDefault(root, "status", "NEW"));
    }

    public async Task<UpvestPortfolioOrder> GetPortfolioOrderAsync(string portfolioOrderId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/portfolios/orders/{portfolioOrderId}", "portfolios:read", null, ct);
        var root = doc!.RootElement;
        return new UpvestPortfolioOrder(root.GetProperty("id").GetString()!, GetStringOrDefault(root, "status", "NEW"));
    }

    // ------------------------------------------------------------- Flow 4

    public async Task<string> CreateRebalancingAsync(string allocationId, CancellationToken ct = default)
    {
        var body = new { allocations = new[] { allocationId } };
        using var doc = await SendAsync(HttpMethod.Post, "/portfolios/rebalancings/executions", "portfolios:admin", body, ct);
        return doc!.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<IReadOnlyList<string>> GetRebalancingOrderStatusesAsync(string executionId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/portfolios/rebalancings/executions/{executionId}/orders", "portfolios:read", null, ct);
        var statuses = new List<string>();
        if (doc!.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var order in data.EnumerateArray())
            {
                var status = GetStringOrDefault(order, "status", "NEW");
                statuses.Add(status);
            }
        }
        return statuses;
    }

    // ------------------------------------------------------------- Flow 5

    public async Task<UpvestValuation> GetValuationAsync(string accountId, CancellationToken ct = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/accounts/{accountId}/valuations/current", "valuations:read", null, ct);
        var root = doc!.RootElement;

        decimal total = 0m;
        if (root.TryGetProperty("total_security_value", out var tsv) && tsv.ValueKind == JsonValueKind.Object
            && tsv.TryGetProperty("amount", out var amt))
        {
            total = ParseAmount(amt);
        }

        var positions = new List<UpvestPosition>();
        if (root.TryGetProperty("security_positions", out var secPositions) && secPositions.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in secPositions.EnumerateArray())
            {
                var isin = p.TryGetProperty("instrument", out var instr) && instr.TryGetProperty("isin", out var isinEl)
                    ? isinEl.GetString() ?? string.Empty
                    : string.Empty;

                var quantity = p.TryGetProperty("quantity", out var q) ? ParseAmount(q) : 0m;

                var value = 0m;
                if (p.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Object
                    && v.TryGetProperty("amount", out var vamt))
                {
                    value = ParseAmount(vamt);
                }

                positions.Add(new UpvestPosition(isin, quantity, value));
            }
        }

        return new UpvestValuation(positions, total);
    }

    // ------------------------------------------------------------- Plumbing

    private async Task<JsonDocument?> SendAsync(HttpMethod method, string path, string scope, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation(UpvestAuthorizationHandler.ScopeHeader, scope);

        if (method != HttpMethod.Get)
        {
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        }

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            request.Content = content;
        }

        using var response = await _client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await UpvestError.FromResponseAsync(response, ct);
        }

        var payload = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(payload) ? null : JsonDocument.Parse(payload);
    }

    private static object[] ToAllocation(IReadOnlyList<FundWeightInput> funds) =>
        funds.Select(f => (object)new
        {
            instrument_id = f.InstrumentId,
            instrument_id_type = "ISIN",
            weight = FormatWeight(f.Weight)
        }).ToArray();

    private static string MapDocumentType(string type) => type?.Trim().ToUpperInvariant() switch
    {
        "PASSPORT" => "PASSPORT",
        "ID_CARD" or "IDCARD" or "NATIONAL_ID" or "ID" => "ID_CARD",
        "RESIDENCE_PERMIT" => "RESIDENCE_PERMIT",
        "DRIVING_LICENSE" or "DRIVERS_LICENSE" or "DRIVING_LICENCE" => "DRIVING_LICENSE",
        _ => "ID_CARD"
    };

    /// <summary>Amounts are euro values with two decimals.</summary>
    private static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Model weights are percentages; Upvest allocation weights are fractions summing to 1.0.</summary>
    private static string FormatWeight(decimal percent) => (percent / 100m).ToString("0.######", CultureInfo.InvariantCulture);

    private static decimal ParseAmount(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m,
        JsonValueKind.Number => element.GetDecimal(),
        _ => 0m
    };

    private static string GetStringOrDefault(JsonElement element, string property, string fallback) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
}
