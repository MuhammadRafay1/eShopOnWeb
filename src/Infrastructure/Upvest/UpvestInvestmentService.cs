using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Upvest;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The concrete Upvest Investment API client. Every call is signed (via the HTTP handler), carries
/// a minimal-scope bearer token, and translates non-success responses into
/// <see cref="UpvestApiException"/>. All requests go to the configured <c>Upvest:BaseUrl</c>.
/// </summary>
public class UpvestInvestmentService : IUpvestInvestmentService
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestTokenProvider _tokenProvider;

    public UpvestInvestmentService(IHttpClientFactory httpClientFactory, UpvestTokenProvider tokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
    }

    public async Task<UpvestUser> CreateUserAsync(UpvestUserRegistration registration, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            first_name = registration.FirstName,
            last_name = registration.LastName,
            email = registration.Email,
            birth_date = registration.BirthDate.ToString("yyyy-MM-dd", Inv),
            birth_city = registration.BirthCity,
            birth_country = registration.BirthCountry,
            nationalities = new[] { registration.Nationality },
            address = new
            {
                address_line1 = registration.Address.Line1,
                postcode = registration.Address.Postcode,
                city = registration.Address.City,
                country = registration.Address.Country
            },
            phone_number = DigitsOnly(registration.PhoneNumber),
            fatca = new
            {
                status = false,
                confirmed_at = Rfc3339Now()
            }
        };

        using var doc = await SendAsync(HttpMethod.Post, "users", UpvestHttp.Scopes.UsersAdmin, "create-user", body, idempotent: true, cancellationToken);
        return ReadUser(doc!.RootElement);
    }

    public async Task<UpvestUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // users:admin is confirmed enabled for this client and covers reads; users:read may not be.
        using var doc = await SendAsync(HttpMethod.Get, $"users/{userId}", UpvestHttp.Scopes.UsersAdmin, "get-user", null, idempotent: false, cancellationToken, allowNotFound: true);
        return doc is null ? null : ReadUser(doc.RootElement);
    }

    public async Task SetTaxResidenciesAsync(string userId, IReadOnlyList<UpvestTaxResidency> residencies, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            tax_residencies = residencies.Select(r => new
            {
                country = r.Country,
                tax_identifier_number = r.TaxIdentifierNumber
            }).ToArray()
        };
        using var _ = await SendAsync(HttpMethod.Post, $"users/{userId}/tax_residencies", UpvestHttp.Scopes.TaxesAdmin, "set-tax-residencies", body, idempotent: true, cancellationToken);
    }

    public async Task<string> CreateKycCheckAsync(string userId, UpvestKycCheck check, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            type = "KYC",
            check_confirmed_at = Rfc3339Now(),
            provider = "eShopOnWeb",
            method = "ID_COLLECTION",
            document_type = check.DocumentType.ToUpperInvariant(),
            document_expiration_date = check.ExpiryDate.ToString("yyyy-MM-dd", Inv),
            nationality = check.IssuingCountry,
            confirmed_address = new
            {
                address_line1 = check.ConfirmedAddress.Line1,
                postcode = check.ConfirmedAddress.Postcode,
                city = check.ConfirmedAddress.City,
                country = check.ConfirmedAddress.Country
            }
        };
        using var doc = await SendAsync(HttpMethod.Post, $"users/{userId}/checks", UpvestHttp.Scopes.ChecksAdmin, "create-kyc-check", body, idempotent: true, cancellationToken);
        return doc!.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken cancellationToken = default)
    {
        var body = new { user_id = userId, type = "PERSONAL" };
        using var doc = await SendAsync(HttpMethod.Post, "account_groups", UpvestHttp.Scopes.AccountsAdmin, "create-account-group", body, idempotent: true, cancellationToken);
        return ReadAccountGroup(doc!.RootElement);
    }

    public async Task<UpvestAccountGroup?> GetAccountGroupAsync(string accountGroupId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"account_groups/{accountGroupId}", UpvestHttp.Scopes.AccountsRead, "get-account-group", null, idempotent: false, cancellationToken, allowNotFound: true);
        return doc is null ? null : ReadAccountGroup(doc.RootElement);
    }

    public async Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, CancellationToken cancellationToken = default)
    {
        var body = new { user_id = userId, account_group_id = accountGroupId, type = "PORTFOLIO" };
        using var doc = await SendAsync(HttpMethod.Post, "accounts", UpvestHttp.Scopes.AccountsAdmin, "create-account", body, idempotent: true, cancellationToken);
        return ReadAccount(doc!.RootElement);
    }

    public async Task<UpvestAccount?> GetAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"accounts/{accountId}", UpvestHttp.Scopes.AccountsRead, "get-account", null, idempotent: false, cancellationToken, allowNotFound: true);
        return doc is null ? null : ReadAccount(doc.RootElement);
    }

    public async Task<string> CreateAllocationAsync(string name, IReadOnlyList<UpvestAllocationEntry> entries, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            name,
            allocation = entries.Select(e => new
            {
                instrument_id = e.InstrumentId,
                instrument_id_type = "ISIN",
                weight = e.Weight.ToString(Inv)
            }).ToArray()
        };
        using var doc = await SendAsync(HttpMethod.Post, "portfolios/allocations", UpvestHttp.Scopes.PortfoliosAdmin, "create-allocation", body, idempotent: true, cancellationToken);
        return doc!.RootElement.GetProperty("id").GetString()!;
    }

    public async Task CreateConfigurationAsync(string accountId, string allocationId, CancellationToken cancellationToken = default)
    {
        var body = new { account_id = accountId, allocation_id = allocationId };
        using var _ = await SendAsync(HttpMethod.Post, "portfolios/configurations", UpvestHttp.Scopes.PortfoliosAdmin, "create-configuration", body, idempotent: true, cancellationToken);
    }

    public async Task<string> CreateBankTransactionAsync(UpvestBankTransaction transaction, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            account = new { iban = UpvestHttp.Sandbox.PoolIban },
            amount = transaction.Amount.ToString("0.00", Inv),
            currency = transaction.Currency,
            client_reference = Guid.NewGuid().ToString(),
            remittance_information = "CF" + transaction.SecuritiesAccountNumber,
            counterparty_name = transaction.CounterpartyName,
            counterparty_account = new { iban = UpvestHttp.Sandbox.CounterpartyIban },
            counterparty_agent = new { bic = UpvestHttp.Sandbox.CounterpartyBic }
        };
        using var doc = await SendAsync(HttpMethod.Post, "tests/payments/transactions", UpvestHttp.Scopes.TestsAdmin, "create-bank-transaction", body, idempotent: true, cancellationToken);
        return doc!.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<UpvestOrder> PlaceOrderAsync(string userId, string accountId, decimal cashAmount, string currency, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            user_id = userId,
            account_id = accountId,
            cash_amount = cashAmount.ToString("0.00", Inv),
            currency,
            side = "BUY"
        };
        using var doc = await SendAsync(HttpMethod.Post, "portfolios/orders", UpvestHttp.Scopes.PortfoliosAdmin, "place-order", body, idempotent: true, cancellationToken);
        return ReadOrder(doc!.RootElement);
    }

    public async Task<UpvestOrder?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"portfolios/orders/{orderId}", UpvestHttp.Scopes.PortfoliosRead, "get-order", null, idempotent: false, cancellationToken, allowNotFound: true);
        return doc is null ? null : ReadOrder(doc.RootElement);
    }

    public async Task<string> TriggerRebalancingByAllocationAsync(string allocationId, CancellationToken cancellationToken = default)
    {
        var body = new { allocations = new[] { allocationId } };
        using var doc = await SendAsync(HttpMethod.Post, "portfolios/rebalancings/executions", UpvestHttp.Scopes.PortfoliosAdmin, "trigger-rebalancing", body, idempotent: true, cancellationToken);
        return doc!.RootElement.GetProperty("id").GetString()!;
    }

    public async Task<IReadOnlyList<UpvestRebalancingOrder>> GetRebalancingOrdersAsync(string executionId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"portfolios/rebalancings/executions/{executionId}/orders", UpvestHttp.Scopes.PortfoliosRead, "get-rebalancing-orders", null, idempotent: false, cancellationToken, allowNotFound: true);
        if (doc is null)
        {
            return Array.Empty<UpvestRebalancingOrder>();
        }

        var orders = new List<UpvestRebalancingOrder>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                orders.Add(new UpvestRebalancingOrder(
                    item.GetProperty("id").GetString() ?? string.Empty,
                    item.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty));
            }
        }
        return orders;
    }

    public async Task<UpvestValuation?> GetAccountValuationAsync(string accountId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"accounts/{accountId}/valuations/current?price_quality=HIGHEST_AVAILABLE", UpvestHttp.Scopes.ValuationsRead, "get-valuation", null, idempotent: false, cancellationToken, allowNotFound: true);
        if (doc is null)
        {
            return null;
        }

        var root = doc.RootElement;
        decimal total = 0m;
        if (root.TryGetProperty("total_security_value", out var tsv) && tsv.TryGetProperty("amount", out var amt))
        {
            total = ParseDecimal(amt);
        }

        var positions = new List<UpvestValuationPosition>();
        if (root.TryGetProperty("security_positions", out var secs) && secs.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in secs.EnumerateArray())
            {
                var isin = p.TryGetProperty("instrument", out var ins) && ins.TryGetProperty("isin", out var i)
                    ? i.GetString() ?? string.Empty : string.Empty;
                var quantity = p.TryGetProperty("quantity", out var q) ? ParseDecimal(q) : 0m;
                decimal? value = null;
                if (p.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Object && v.TryGetProperty("amount", out var va))
                {
                    value = ParseDecimal(va);
                }
                positions.Add(new UpvestValuationPosition(isin, quantity, value));
            }
        }

        return new UpvestValuation(total, positions);
    }

    // ----- plumbing ------------------------------------------------------------------------

    private async Task<JsonDocument?> SendAsync(
        HttpMethod method, string path, string scope, string operation, object? body,
        bool idempotent, CancellationToken cancellationToken, bool allowNotFound = false)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(scope, cancellationToken);
        var client = _httpClientFactory.CreateClient(UpvestHttp.ClientName);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotent)
        {
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        }
        if (body is not null)
        {
            // Upvest expects exactly "application/json" (no charset parameter).
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound && allowNotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UpvestApiException((int)response.StatusCode, operation, ExtractProblemDetail(payload));
        }

        return string.IsNullOrWhiteSpace(payload) ? null : JsonDocument.Parse(payload);
    }

    private static string Rfc3339Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);

    /// <summary>Upvest's phone_number accepts digits only (E.164 without the leading '+').</summary>
    private static string? DigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static decimal ParseDecimal(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Any, Inv, out var d) ? d : 0m,
        JsonValueKind.Number => element.GetDecimal(),
        _ => 0m
    };

    private static UpvestUser ReadUser(JsonElement e) =>
        new(e.GetProperty("id").GetString()!, GetString(e, "status"));

    private static UpvestAccountGroup ReadAccountGroup(JsonElement e) =>
        new(e.GetProperty("id").GetString()!, GetString(e, "status"),
            e.TryGetProperty("securities_account_number", out var s) ? s.GetString() : null);

    private static UpvestAccount ReadAccount(JsonElement e) =>
        new(e.GetProperty("id").GetString()!, GetString(e, "status"));

    private static UpvestOrder ReadOrder(JsonElement e) =>
        new(e.GetProperty("id").GetString()!, GetString(e, "status"));

    private static string GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    /// <summary>Extracts the non-PII 'detail'/'title' from a problem+json body, if any.</summary>
    private static string? ExtractProblemDetail(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
            {
                return d.GetString();
            }
            if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
            {
                return t.GetString();
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body (e.g. the token endpoint's plain message); ignore.
        }
        return null;
    }
}
