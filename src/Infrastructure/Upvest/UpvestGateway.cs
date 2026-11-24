using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// <see cref="IUpvestGateway"/> implementation over the Upvest Investment API. Every call acquires a
/// scoped access token, is signed by <see cref="UpvestSigningHandler"/>, and translates the Upvest
/// response (or problem+json error) into the shop's domain types.
/// </summary>
public sealed class UpvestGateway : IUpvestGateway
{
    public const string HttpClientName = "upvest-api";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Upvest's schemas reject explicit nulls for optional fields; omit them instead.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly UpvestTokenService _tokens;

    public UpvestGateway(HttpClient http, UpvestTokenService tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    // --------------------------------------------------------------- Investor onboarding

    public async Task<UpvestUser> CreateUserAsync(InvestorSignUp s, CancellationToken ct = default)
    {
        var body = new CreateUserRequest
        {
            FirstName = s.FirstName,
            LastName = s.LastName,
            Email = s.Email,
            BirthDate = s.BirthDate,
            BirthCity = s.BirthCity,
            BirthCountry = s.BirthCountry,
            Nationalities = new[] { s.Nationality },
            PhoneNumber = SanitisePhone(s.PhoneNumber),
            Address = new AddressDto
            {
                AddressLine1 = s.Address.Line1,
                Postcode = s.Address.Postcode,
                City = s.Address.City,
                Country = s.Address.Country,
            },
            Fatca = new FatcaDto { Status = false, ConfirmedAt = Now() },
        };

        var user = await SendAsync<UserResponse>(HttpMethod.Post, "/users", UpvestScopes.UsersAdmin, body, "create_user", idempotent: true, ct);
        return new UpvestUser(user.Id, user.Status);
    }

    public async Task<UpvestUser> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await SendAsync<UserResponse>(HttpMethod.Get, $"/users/{userId}", UpvestScopes.UsersAdmin, null, "get_user", idempotent: false, ct);
        return new UpvestUser(user.Id, user.Status);
    }

    public Task SetTaxResidenciesAsync(Guid userId, string taxCountry, string taxIdentifier, CancellationToken ct = default)
    {
        var body = new SetTaxResidenciesRequest
        {
            TaxResidencies = new[]
            {
                new TaxResidencyDto { Country = taxCountry, TaxIdentifierNumber = taxIdentifier }
            }
        };
        return SendAsync(HttpMethod.Post, $"/users/{userId}/tax_residencies", UpvestScopes.TaxesAdmin, body, "set_tax_residencies", idempotent: true, ct);
    }

    public Task CreateIdentifierAsync(Guid userId, IdentityDocument document, CancellationToken ct = default)
    {
        var type = document.Type.ToUpperInvariant().Contains("PASSPORT") ? "PASSPORT" : "NATIONAL_ID";
        var body = new CreateIdentifierRequest
        {
            Type = type,
            IssuingCountry = document.IssuingCountry,
            Identifier = document.Number,
        };
        return SendAsync(HttpMethod.Post, $"/users/{userId}/identifiers", UpvestScopes.UsersAdmin, body, "create_identifier", idempotent: true, ct);
    }

    public Task CreateKycCheckAsync(Guid userId, IdentityDocument document, string nationality, CancellationToken ct = default)
    {
        var body = new CreateKycCheckRequest
        {
            Type = "KYC",
            CheckConfirmedAt = Now(),
            DataDownloadLink = "https://eshoponweb.example/kyc-evidence",
            DocumentType = MapDocumentType(document.Type),
            DocumentExpirationDate = document.ExpiryDate,
            Nationality = nationality,
            Provider = "eShopOnWeb",
            Method = "ELECTRONIC_ID",
        };
        return SendAsync(HttpMethod.Post, $"/users/{userId}/checks", UpvestScopes.ChecksAdmin, body, "create_check", idempotent: true, ct);
    }

    public async Task<UpvestAccountGroup> CreateAccountGroupAsync(Guid userId, CancellationToken ct = default)
    {
        var body = new CreateAccountGroupRequest { UserId = userId, Type = "PERSONAL" };
        var group = await SendAsync<AccountGroupResponse>(HttpMethod.Post, "/account_groups", UpvestScopes.AccountsAdmin, body, "create_account_group", idempotent: true, ct);
        return new UpvestAccountGroup(group.Id, group.Status);
    }

    public async Task<UpvestAccount> CreateAccountAsync(Guid userId, Guid accountGroupId, CancellationToken ct = default)
    {
        var body = new CreateAccountRequest { UserId = userId, AccountGroupId = accountGroupId, Type = "PORTFOLIO" };
        var account = await SendAsync<AccountResponse>(HttpMethod.Post, "/accounts", UpvestScopes.AccountsAdmin, body, "create_account", idempotent: true, ct);
        return new UpvestAccount(account.Id, account.Status);
    }

    public async Task<UpvestAccount> GetAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await SendAsync<AccountResponse>(HttpMethod.Get, $"/accounts/{accountId}", UpvestScopes.AccountsRead, null, "get_account", idempotent: false, ct);
        return new UpvestAccount(account.Id, account.Status);
    }

    // --------------------------------------------------------------- Model portfolios

    public async Task<UpvestAllocation> CreateAllocationAsync(string name, IEnumerable<AllocationFund> funds, CancellationToken ct = default)
    {
        var body = new AllocationRequest { Name = name, Allocation = ToAllocationItems(funds) };
        var allocation = await SendAsync<AllocationResponse>(HttpMethod.Post, "/portfolios/allocations", UpvestScopes.PortfoliosAdmin, body, "create_allocation", idempotent: true, ct);
        return new UpvestAllocation(allocation.Id, allocation.Name ?? name, funds.ToList());
    }

    public async Task<UpvestAllocation> UpdateAllocationAsync(Guid allocationId, string name, IEnumerable<AllocationFund> funds, CancellationToken ct = default)
    {
        var body = new AllocationRequest { Name = name, Allocation = ToAllocationItems(funds) };
        var allocation = await SendAsync<AllocationResponse>(HttpMethod.Put, $"/portfolios/allocations/{allocationId}", UpvestScopes.PortfoliosAdmin, body, "update_allocation", idempotent: true, ct);
        return new UpvestAllocation(allocation.Id, allocation.Name ?? name, funds.ToList());
    }

    public Task CreateConfigurationAsync(Guid accountId, Guid allocationId, CancellationToken ct = default)
    {
        var body = new CreateConfigurationRequest { AccountId = accountId, AllocationId = allocationId };
        return SendAsync(HttpMethod.Post, "/portfolios/configurations", UpvestScopes.PortfoliosAdmin, body, "create_configuration", idempotent: true, ct);
    }

    public Task UpdateConfigurationAsync(Guid accountId, Guid allocationId, CancellationToken ct = default)
    {
        var body = new UpdateConfigurationRequest { AllocationId = allocationId };
        return SendAsync(HttpMethod.Patch, $"/portfolios/configurations/{accountId}", UpvestScopes.PortfoliosAdmin, body, "update_configuration", idempotent: true, ct);
    }

    // --------------------------------------------------------------- Paying in

    public async Task<UpvestVirtualCashIncrease> IncreaseVirtualCashAsync(Guid accountGroupId, decimal amount, string currency, CancellationToken ct = default)
    {
        var body = new VirtualCashIncreaseRequest
        {
            AccountGroupId = accountGroupId,
            Amount = Money(amount),
            Currency = currency,
        };
        var increase = await SendAsync<VirtualCashIncreaseResponse>(HttpMethod.Post, "/virtual_cash_balances/increases", UpvestScopes.VirtualCashAdmin, body, "virtual_cash_increase", idempotent: true, ct);
        return new UpvestVirtualCashIncrease(increase.Id, increase.Status);
    }

    public async Task<UpvestPortfolioOrder> CreatePortfolioOrderAsync(Guid userId, Guid accountId, decimal amount, string currency, string side, CancellationToken ct = default)
    {
        var body = new PortfolioOrderRequest
        {
            UserId = userId,
            AccountId = accountId,
            CashAmount = Money(amount),
            Currency = currency,
            Side = side,
        };
        var order = await SendAsync<PortfolioOrderResponse>(HttpMethod.Post, "/portfolios/orders", UpvestScopes.PortfoliosAdmin, body, "create_portfolio_order", idempotent: true, ct);
        return new UpvestPortfolioOrder(order.Id, order.Status);
    }

    public async Task<UpvestPortfolioOrder> GetPortfolioOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await SendAsync<PortfolioOrderResponse>(HttpMethod.Get, $"/portfolios/orders/{orderId}", UpvestScopes.PortfoliosRead, null, "get_portfolio_order", idempotent: false, ct);
        return new UpvestPortfolioOrder(order.Id, order.Status);
    }

    // --------------------------------------------------------------- Realignment

    public async Task<Guid> TriggerRebalancingAsync(Guid allocationId, CancellationToken ct = default)
    {
        var body = new TriggerRebalancingRequest
        {
            Accounts = Array.Empty<Guid>(),
            Allocations = new[] { allocationId },
        };
        var execution = await SendAsync<RebalancingExecutionResponse>(HttpMethod.Post, "/portfolios/rebalancings/executions", UpvestScopes.PortfoliosAdmin, body, "trigger_rebalancing", idempotent: true, ct);
        return execution.Id;
    }

    public async Task<UpvestRebalancingExecution> GetRebalancingExecutionAsync(Guid executionId, CancellationToken ct = default)
    {
        var orders = await SendAsync<RebalancingOrdersResponse>(
            HttpMethod.Get, $"/portfolios/rebalancings/executions/{executionId}/orders", UpvestScopes.PortfoliosRead, null, "get_rebalancing_orders", idempotent: false, ct);

        var statuses = orders.Data?.Select(o => o.Status ?? string.Empty).ToList() ?? new List<string>();
        return new UpvestRebalancingExecution(executionId, statuses);
    }

    // --------------------------------------------------------------- Portfolio valuation

    public async Task<UpvestValuation> GetValuationAsync(Guid accountId, CancellationToken ct = default)
    {
        var valuation = await SendAsync<ValuationResponse>(
            HttpMethod.Get, $"/accounts/{accountId}/valuations/current", UpvestScopes.ValuationsRead, null, "get_valuation", idempotent: false, ct);

        var holdings = (valuation.SecurityPositions ?? new List<SecurityPositionDto>())
            .Select(p => new UpvestHolding(
                p.Instrument?.Isin ?? string.Empty,
                ParseDecimal(p.Quantity),
                ParseDecimal(p.Value?.Amount)))
            .ToList();

        return new UpvestValuation(
            ParseDecimal(valuation.TotalSecurityValue?.Amount),
            valuation.TotalSecurityValue?.Currency ?? "EUR",
            holdings);
    }

    // --------------------------------------------------------------- Transport

    private async Task<T> SendAsync<T>(HttpMethod method, string path, string scope, object? body, string operation, bool idempotent, CancellationToken ct)
    {
        using var response = await SendCoreAsync(method, path, scope, body, operation, idempotent, ct);
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return result ?? throw new UpvestApiException(operation, (int)response.StatusCode, "Empty response body.");
    }

    private async Task SendAsync(HttpMethod method, string path, string scope, object? body, string operation, bool idempotent, CancellationToken ct)
    {
        using var _ = await SendCoreAsync(method, path, scope, body, operation, idempotent, ct);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, string scope, object? body, string operation, bool idempotent, CancellationToken ct)
    {
        var token = await _tokens.GetTokenAsync(scope, ct);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        if (idempotent)
        {
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        }

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            request.Content = content;
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is not UpvestApiException)
        {
            throw new UpvestApiException(operation, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await UpvestProblem.ReadDetailAsync(response, ct);
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new UpvestApiException(operation, status, detail);
        }

        return response;
    }

    // --------------------------------------------------------------- Helpers

    private static List<AllocationItemDto> ToAllocationItems(IEnumerable<AllocationFund> funds) =>
        funds.Select(f => new AllocationItemDto
        {
            InstrumentId = f.Isin,
            InstrumentIdType = "ISIN",
            Weight = Money(f.Weight),
        }).ToList();

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static string? SanitisePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }
        var sb = new StringBuilder(phone.Length);
        foreach (var c in phone)
        {
            if (char.IsDigit(c))
            {
                sb.Append(c);
            }
        }
        // Upvest requires 8-15 digits (or the field omitted); drop anything else rather than 400.
        return sb.Length is >= 8 and <= 15 ? sb.ToString() : null;
    }

    private static string MapDocumentType(string type) => type.ToUpperInvariant() switch
    {
        var t when t.Contains("PASSPORT") => "PASSPORT",
        var t when t.Contains("DRIV") => "DRIVING_LICENSE",
        var t when t.Contains("RESIDENCE") => "RESIDENCE_PERMIT",
        _ => "ID_CARD",
    };

    // --------------------------------------------------------------- DTOs

    private sealed class CreateUserRequest
    {
        [JsonPropertyName("first_name")] public string FirstName { get; set; } = string.Empty;
        [JsonPropertyName("last_name")] public string LastName { get; set; } = string.Empty;
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("birth_date")] public string BirthDate { get; set; } = string.Empty;
        [JsonPropertyName("birth_city")] public string? BirthCity { get; set; }
        [JsonPropertyName("birth_country")] public string? BirthCountry { get; set; }
        [JsonPropertyName("nationalities")] public string[] Nationalities { get; set; } = Array.Empty<string>();
        [JsonPropertyName("phone_number")] public string? PhoneNumber { get; set; }
        [JsonPropertyName("address")] public AddressDto Address { get; set; } = new();
        [JsonPropertyName("fatca")] public FatcaDto Fatca { get; set; } = new();
    }

    private sealed class AddressDto
    {
        [JsonPropertyName("address_line1")] public string AddressLine1 { get; set; } = string.Empty;
        [JsonPropertyName("postcode")] public string Postcode { get; set; } = string.Empty;
        [JsonPropertyName("city")] public string City { get; set; } = string.Empty;
        [JsonPropertyName("country")] public string Country { get; set; } = string.Empty;
    }

    private sealed class FatcaDto
    {
        [JsonPropertyName("status")] public bool Status { get; set; }
        [JsonPropertyName("confirmed_at")] public string ConfirmedAt { get; set; } = string.Empty;
    }

    private sealed class UserResponse
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    }

    private sealed class SetTaxResidenciesRequest
    {
        [JsonPropertyName("tax_residencies")] public TaxResidencyDto[] TaxResidencies { get; set; } = Array.Empty<TaxResidencyDto>();
    }

    private sealed class TaxResidencyDto
    {
        [JsonPropertyName("country")] public string Country { get; set; } = string.Empty;
        [JsonPropertyName("tax_identifier_number")] public string TaxIdentifierNumber { get; set; } = string.Empty;
    }

    private sealed class CreateIdentifierRequest
    {
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("issuing_country")] public string IssuingCountry { get; set; } = string.Empty;
        [JsonPropertyName("identifier")] public string Identifier { get; set; } = string.Empty;
    }

    private sealed class CreateKycCheckRequest
    {
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("check_confirmed_at")] public string CheckConfirmedAt { get; set; } = string.Empty;
        [JsonPropertyName("data_download_link")] public string DataDownloadLink { get; set; } = string.Empty;
        [JsonPropertyName("document_type")] public string DocumentType { get; set; } = string.Empty;
        [JsonPropertyName("document_expiration_date")] public string? DocumentExpirationDate { get; set; }
        [JsonPropertyName("nationality")] public string? Nationality { get; set; }
        [JsonPropertyName("provider")] public string Provider { get; set; } = string.Empty;
        [JsonPropertyName("method")] public string Method { get; set; } = string.Empty;
    }

    private sealed class CreateAccountGroupRequest
    {
        [JsonPropertyName("user_id")] public Guid UserId { get; set; }
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    }

    private sealed class AccountGroupResponse
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    }

    private sealed class CreateAccountRequest
    {
        [JsonPropertyName("user_id")] public Guid UserId { get; set; }
        [JsonPropertyName("account_group_id")] public Guid AccountGroupId { get; set; }
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    }

    private sealed class AccountResponse
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    }

    private sealed class AllocationRequest
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("allocation")] public List<AllocationItemDto> Allocation { get; set; } = new();
    }

    private sealed class AllocationItemDto
    {
        [JsonPropertyName("instrument_id")] public string InstrumentId { get; set; } = string.Empty;
        [JsonPropertyName("instrument_id_type")] public string InstrumentIdType { get; set; } = "ISIN";
        [JsonPropertyName("weight")] public string Weight { get; set; } = "0";
    }

    private sealed class AllocationResponse
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class CreateConfigurationRequest
    {
        [JsonPropertyName("account_id")] public Guid AccountId { get; set; }
        [JsonPropertyName("allocation_id")] public Guid AllocationId { get; set; }
    }

    private sealed class UpdateConfigurationRequest
    {
        [JsonPropertyName("allocation_id")] public Guid AllocationId { get; set; }
    }

    private sealed class VirtualCashIncreaseRequest
    {
        [JsonPropertyName("account_group_id")] public Guid AccountGroupId { get; set; }
        [JsonPropertyName("amount")] public string Amount { get; set; } = "0.00";
        [JsonPropertyName("currency")] public string Currency { get; set; } = "EUR";
    }

    private sealed class VirtualCashIncreaseResponse
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    }

    private sealed class PortfolioOrderRequest
    {
        [JsonPropertyName("user_id")] public Guid UserId { get; set; }
        [JsonPropertyName("account_id")] public Guid AccountId { get; set; }
        [JsonPropertyName("cash_amount")] public string CashAmount { get; set; } = "0.00";
        [JsonPropertyName("currency")] public string Currency { get; set; } = "EUR";
        [JsonPropertyName("side")] public string Side { get; set; } = "BUY";
    }

    private sealed class PortfolioOrderResponse
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    }

    private sealed class TriggerRebalancingRequest
    {
        [JsonPropertyName("accounts")] public Guid[] Accounts { get; set; } = Array.Empty<Guid>();
        [JsonPropertyName("allocations")] public Guid[] Allocations { get; set; } = Array.Empty<Guid>();
    }

    private sealed class RebalancingExecutionResponse
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
    }

    private sealed class RebalancingOrdersResponse
    {
        [JsonPropertyName("data")] public List<RebalancingOrderDto>? Data { get; set; }
    }

    private sealed class RebalancingOrderDto
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
    }

    private sealed class ValuationResponse
    {
        [JsonPropertyName("total_security_value")] public MoneyDto? TotalSecurityValue { get; set; }
        [JsonPropertyName("security_positions")] public List<SecurityPositionDto>? SecurityPositions { get; set; }
    }

    private sealed class SecurityPositionDto
    {
        [JsonPropertyName("instrument")] public InstrumentDto? Instrument { get; set; }
        [JsonPropertyName("quantity")] public string? Quantity { get; set; }
        [JsonPropertyName("value")] public MoneyDto? Value { get; set; }
    }

    private sealed class InstrumentDto
    {
        [JsonPropertyName("isin")] public string? Isin { get; set; }
    }

    private sealed class MoneyDto
    {
        [JsonPropertyName("amount")] public string? Amount { get; set; }
        [JsonPropertyName("currency")] public string? Currency { get; set; }
    }
}
