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
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// HTTP implementation of <see cref="IUpvestClient"/>. Every request is scoped (OAuth2) and
/// HTTP-message-signed, carries an idempotency key for mutations, and is retried on transient
/// failures. Responses are mapped onto the shop's domain types; personal data is never logged.
/// </summary>
public class UpvestClient : IUpvestClient
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestTokenProvider _tokenProvider;
    private readonly UpvestMessageSigner _signer;
    private readonly UpvestOptions _options;
    private readonly IAppLogger<UpvestClient> _logger;

    public UpvestClient(
        IHttpClientFactory httpClientFactory,
        UpvestTokenProvider tokenProvider,
        UpvestMessageSigner signer,
        IOptions<UpvestOptions> options,
        IAppLogger<UpvestClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _signer = signer;
        _options = options.Value;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Flow 1

    public async Task<UpvestUser> CreateUserAsync(InvestorRegistration r, CancellationToken ct)
    {
        var body = new CreateUserRequest(
            FirstName: r.FirstName,
            LastName: r.LastName,
            Email: r.Email,
            BirthDate: r.BirthDate,
            BirthCity: r.BirthCity,
            BirthCountry: r.BirthCountry,
            Nationalities: new[] { r.Nationality },
            PhoneNumber: DigitsOnly(r.PhoneNumber),
            Address: new AddressDto(r.Address.Line1, r.Address.Postcode, r.Address.City, r.Address.Country),
            Fatca: new FatcaDto(false, UtcNow()));

        using var doc = await SendJsonAsync(HttpMethod.Post, "/users", UpvestHttp.Scopes.Users, body, idempotent: true, "users.create", ct);
        return ReadUser(doc.RootElement);
    }

    public async Task<UpvestUser> GetUserAsync(string upvestUserId, CancellationToken ct)
    {
        using var doc = await SendJsonAsync(HttpMethod.Get, $"/users/{upvestUserId}", UpvestHttp.Scopes.Users, null, idempotent: false, "users.get", ct);
        return ReadUser(doc.RootElement);
    }

    public async Task SetTaxResidencyAsync(string upvestUserId, string taxCountry, string taxId, CancellationToken ct)
    {
        var body = new TaxResidenciesRequest(new[] { new TaxResidencyDto(taxCountry, taxId) });
        using var _ = await SendJsonAsync(HttpMethod.Post, $"/users/{upvestUserId}/tax_residencies", UpvestHttp.Scopes.Taxes, body, idempotent: true, "users.tax_residencies", ct);
    }

    public async Task SubmitIdentityCheckAsync(string upvestUserId, InvestorRegistration r, CancellationToken ct)
    {
        var body = new KycCheckRequest(
            Type: "KYC",
            CheckConfirmedAt: UtcNow(),
            DataDownloadLink: "https://eshoponweb.example/identity-verifications/" + upvestUserId,
            DocumentType: MapDocumentType(r.IdentityDocument.Type),
            DocumentExpirationDate: r.IdentityDocument.ExpiryDate,
            Nationality: r.Nationality,
            Provider: "eShopOnWeb",
            Method: "ELECTRONIC_ID");
        using var _ = await SendJsonAsync(HttpMethod.Post, $"/users/{upvestUserId}/checks", UpvestHttp.Scopes.Checks, body, idempotent: true, "users.checks", ct);
    }

    public async Task<string> CreateAccountGroupAsync(string upvestUserId, CancellationToken ct)
    {
        var body = new CreateAccountGroupRequest(upvestUserId, "PERSONAL");
        using var doc = await SendJsonAsync(HttpMethod.Post, "/account_groups", UpvestHttp.Scopes.Accounts, body, idempotent: true, "account_groups.create", ct);
        return GetString(doc.RootElement, "id")!;
    }

    public async Task<UpvestAccount> CreateAccountAsync(string upvestUserId, string accountGroupId, CancellationToken ct)
    {
        var body = new CreateAccountRequest(upvestUserId, accountGroupId, "PORTFOLIO", "Managed portfolio");
        using var doc = await SendJsonAsync(HttpMethod.Post, "/accounts", UpvestHttp.Scopes.Accounts, body, idempotent: true, "accounts.create", ct);
        return ReadAccount(doc.RootElement);
    }

    public async Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken ct)
    {
        using var doc = await SendJsonAsync(HttpMethod.Get, $"/accounts/{accountId}", UpvestHttp.Scopes.Accounts, null, idempotent: false, "accounts.get", ct);
        return ReadAccount(doc.RootElement);
    }

    // ---------------------------------------------------------------- Flow 2

    public async Task<string> CreateAllocationAsync(string name, IReadOnlyList<FundWeight> funds, CancellationToken ct)
    {
        var body = new AllocationRequest(name, ToAllocationItems(funds));
        using var doc = await SendJsonAsync(HttpMethod.Post, "/portfolios/allocations", UpvestHttp.Scopes.PortfoliosAdmin, body, idempotent: true, "allocations.create", ct);
        return GetString(doc.RootElement, "id")!;
    }

    public async Task ReplaceAllocationAsync(string allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct)
    {
        var body = new AllocationRequest(name, ToAllocationItems(funds));
        using var _ = await SendJsonAsync(HttpMethod.Put, $"/portfolios/allocations/{allocationId}", UpvestHttp.Scopes.PortfoliosAdmin, body, idempotent: true, "allocations.replace", ct);
    }

    public async Task SetAccountAllocationAsync(string accountId, string allocationId, CancellationToken ct)
    {
        var body = new ConfigurationRequest(accountId, allocationId);
        using var _ = await SendJsonAsync(HttpMethod.Post, "/portfolios/configurations", UpvestHttp.Scopes.PortfoliosAdmin, body, idempotent: true, "configurations.create", ct);
    }

    // ---------------------------------------------------------------- Flow 3

    public async Task<string> CreateTopupAsync(string accountGroupId, decimal amount, string currency, CancellationToken ct)
    {
        var body = new TopupRequest(accountGroupId, Money(amount), currency);
        using var doc = await SendJsonAsync(HttpMethod.Post, "/payments/topups", UpvestHttp.Scopes.Topups, body, idempotent: true, "topups.create", ct);
        return GetString(doc.RootElement, "id")!;
    }

    public async Task<UpvestOrder> CreatePortfolioBuyOrderAsync(string upvestUserId, string accountId, decimal amount, string currency, CancellationToken ct)
    {
        var body = new PortfolioOrderRequest(upvestUserId, accountId, "BUY", Money(amount), currency);
        using var doc = await SendJsonAsync(HttpMethod.Post, "/portfolios/orders", UpvestHttp.Scopes.PortfoliosAdmin, body, idempotent: true, "orders.create", ct);
        return ReadOrder(doc.RootElement);
    }

    public async Task<UpvestOrder> GetPortfolioOrderAsync(string orderId, CancellationToken ct)
    {
        using var doc = await SendJsonAsync(HttpMethod.Get, $"/portfolios/orders/{orderId}", UpvestHttp.Scopes.PortfoliosRead, null, idempotent: false, "orders.get", ct);
        return ReadOrder(doc.RootElement);
    }

    // ---------------------------------------------------------------- Flow 4

    public async Task<string> TriggerRealignmentAsync(string allocationId, CancellationToken ct)
    {
        var body = new RebalancingRequest(new[] { allocationId });
        using var doc = await SendJsonAsync(HttpMethod.Post, "/portfolios/rebalancings/executions", UpvestHttp.Scopes.PortfoliosAdmin, body, idempotent: true, "rebalancings.create", ct);
        return GetString(doc.RootElement, "id")!;
    }

    public async Task<UpvestRebalancing> GetRealignmentAsync(string executionId, CancellationToken ct)
    {
        using var doc = await SendJsonAsync(HttpMethod.Get, $"/portfolios/rebalancings/executions/{executionId}", UpvestHttp.Scopes.PortfoliosRead, null, idempotent: false, "rebalancings.get", ct);
        var id = GetString(doc.RootElement, "id") ?? executionId;

        // The execution object carries no status of its own; derive it from its orders.
        var status = await DeriveRealignmentStatusAsync(executionId, ct);
        return new UpvestRebalancing(id, status);
    }

    private async Task<string> DeriveRealignmentStatusAsync(string executionId, CancellationToken ct)
    {
        try
        {
            using var doc = await SendJsonAsync(HttpMethod.Get, $"/portfolios/rebalancings/executions/{executionId}/orders", UpvestHttp.Scopes.PortfoliosRead, null, idempotent: false, "rebalancings.orders", ct);
            var orders = EnumerateItems(doc.RootElement).ToList();
            if (orders.Count == 0)
            {
                return "PENDING";
            }

            var statuses = orders.Select(o => GetString(o, "status") ?? "").ToList();
            if (statuses.Any(s => s is "CANCELLED" or "FAILED"))
            {
                return "FAILED";
            }
            if (statuses.All(s => s is "FILLED" or "SETTLED"))
            {
                return "COMPLETED";
            }
            return "PENDING";
        }
        catch (UpvestApiException)
        {
            return "PENDING";
        }
    }

    // ---------------------------------------------------------------- Flow 5

    public async Task<UpvestValuation> GetValuationAsync(string accountId, CancellationToken ct)
    {
        using var doc = await SendJsonAsync(HttpMethod.Get, $"/accounts/{accountId}/valuations/current", UpvestHttp.Scopes.ValuationsRead, null, idempotent: false, "valuations.current", ct);
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
            foreach (var pos in positions.EnumerateArray())
            {
                string isin = pos.TryGetProperty("instrument", out var inst)
                    ? GetString(inst, "isin") ?? GetString(inst, "uuid") ?? ""
                    : "";
                decimal quantity = ParseDecimal(GetRawScalar(pos, "quantity"));
                decimal value = pos.TryGetProperty("value", out var val) ? ParseDecimal(GetString(val, "amount")) : 0m;
                holdings.Add(new UpvestHolding(isin, quantity, value));
            }
        }

        return new UpvestValuation(total, currency, holdings);
    }

    // ---------------------------------------------------------------- HTTP core

    private async Task<JsonDocument> SendJsonAsync(
        HttpMethod method, string path, string scope, object? body, bool idempotent, string operation, CancellationToken ct)
    {
        byte[]? bodyBytes = body is null ? null : JsonSerializer.SerializeToUtf8Bytes(body, UpvestHttp.Json);
        var idempotencyKey = idempotent ? Guid.NewGuid().ToString() : null;

        const int maxAttempts = 3;
        bool reauthoried = false;

        for (int attempt = 1; ; attempt++)
        {
            var token = await _tokenProvider.GetTokenAsync(scope, ct);
            using var response = await SendOnceAsync(method, path, bodyBytes, token, idempotencyKey, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return string.IsNullOrWhiteSpace(payload)
                    ? JsonDocument.Parse("{}")
                    : JsonDocument.Parse(payload);
            }

            var status = (int)response.StatusCode;
            var detail = UpvestHttp.ExtractDetail(payload);

            // One re-authentication attempt for a genuinely expired token (never for a scope refusal).
            if (status == 401 && !reauthoried && !IsScopeRefusal(detail))
            {
                reauthoried = true;
                _tokenProvider.Invalidate(scope);
                continue;
            }

            if (IsTransient(response.StatusCode) && attempt < maxAttempts)
            {
                await Task.Delay(RetryDelay(response.StatusCode, attempt), ct);
                continue;
            }

            throw new UpvestApiException(status, operation, detail);
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method, string path, byte[]? bodyBytes, string token, string? idempotencyKey, CancellationToken ct)
    {
        var headers = _signer.Build(
            method: method.Method,
            path: path,
            query: null,
            body: bodyBytes,
            bearerToken: token,
            contentType: "application/json",
            idempotencyKey: idempotencyKey);

        var request = new HttpRequestMessage(method, new Uri(new Uri(_options.BaseUrl, UriKind.Absolute), path));
        if (bodyBytes is not null)
        {
            request.Content = new ByteArrayContent(bodyBytes);
            request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        }
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        request.Headers.TryAddWithoutValidation("authorization", "Bearer " + token);
        request.Headers.TryAddWithoutValidation("upvest-client-id", _options.ClientId);
        if (headers.Digest is not null)
        {
            request.Headers.TryAddWithoutValidation("digest", headers.Digest);
        }
        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("idempotency-key", idempotencyKey);
        }
        request.Headers.TryAddWithoutValidation("signature", headers.Signature);
        request.Headers.TryAddWithoutValidation("signature-input", headers.SignatureInput);

        var client = _httpClientFactory.CreateClient(UpvestHttp.ClientName);
        return await client.SendAsync(request, ct);
    }

    private static bool IsTransient(HttpStatusCode code) =>
        code == HttpStatusCode.TooManyRequests ||       // 429
        code == HttpStatusCode.InternalServerError ||   // 500
        code == HttpStatusCode.ServiceUnavailable;      // 503

    private static TimeSpan RetryDelay(HttpStatusCode code, int attempt) => code switch
    {
        HttpStatusCode.ServiceUnavailable => TimeSpan.FromSeconds(3 * attempt),
        _ => TimeSpan.FromSeconds(2 * attempt),
    };

    private static bool IsScopeRefusal(string? detail) =>
        detail is not null && detail.Contains("not authorised to use these scopes", StringComparison.OrdinalIgnoreCase);

    // ---------------------------------------------------------------- Mapping helpers

    private static UpvestUser ReadUser(JsonElement e) =>
        new(GetString(e, "id") ?? "", GetString(e, "status") ?? "INACTIVE");

    private static UpvestAccount ReadAccount(JsonElement e) =>
        new(GetString(e, "id") ?? "", GetString(e, "status") ?? "PENDING_APPROVAL");

    private static UpvestOrder ReadOrder(JsonElement e) =>
        new(GetString(e, "id") ?? "", GetString(e, "status") ?? "NEW");

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement root)
    {
        foreach (var key in new[] { "orders", "data", "items" })
        {
            if (root.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return arr.EnumerateArray().ToList();
            }
        }
        return root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : Enumerable.Empty<JsonElement>();
    }

    private static AllocationItem[] ToAllocationItems(IReadOnlyList<FundWeight> funds) =>
        funds.Select(f => new AllocationItem(f.InstrumentId, "ISIN", (f.WeightPercent / 100m).ToString("0.########", Inv))).ToArray();

    private static string MapDocumentType(string type) => type.Trim().ToUpperInvariant() switch
    {
        "PASSPORT" => "PASSPORT",
        "ID_CARD" or "IDCARD" or "ID" or "NATIONAL_ID" => "ID_CARD",
        "RESIDENCE_PERMIT" => "RESIDENCE_PERMIT",
        "DRIVING_LICENSE" or "DRIVING_LICENCE" or "DRIVERS_LICENSE" => "DRIVING_LICENSE",
        _ => "PASSPORT",
    };

    private static string? GetString(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
            : null;

    private static string? GetRawScalar(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v.ToString() : null;

    private static decimal ParseDecimal(string? s) =>
        decimal.TryParse(s, NumberStyles.Any, Inv, out var d) ? d : 0m;

    private static string Money(decimal amount) => amount.ToString("0.00", Inv);

    private static string DigitsOnly(string? value) =>
        new string((value ?? "").Where(char.IsDigit).ToArray());

    private static string UtcNow() => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", Inv);

    // ---------------------------------------------------------------- Request DTOs (snake_case on the wire)

    private record CreateUserRequest(
        string FirstName, string LastName, string Email, string BirthDate, string BirthCity,
        string BirthCountry, string[] Nationalities, string PhoneNumber, AddressDto Address, FatcaDto Fatca);

    private record AddressDto(string AddressLine1, string Postcode, string City, string Country);

    private record FatcaDto(bool Status, string ConfirmedAt);

    private record TaxResidenciesRequest(TaxResidencyDto[] TaxResidencies);

    private record TaxResidencyDto(string Country, string TaxIdentifierNumber);

    private record KycCheckRequest(
        string Type, string CheckConfirmedAt, string DataDownloadLink, string DocumentType,
        string DocumentExpirationDate, string Nationality, string Provider, string Method);

    private record CreateAccountGroupRequest(string UserId, string Type);

    private record CreateAccountRequest(string UserId, string AccountGroupId, string Type, string Name);

    private record AllocationRequest(string Name, AllocationItem[] Allocation);

    private record AllocationItem(string InstrumentId, string InstrumentIdType, string Weight);

    private record ConfigurationRequest(string AccountId, string AllocationId);

    private record TopupRequest(string AccountGroupId, string CashAmount, string Currency);

    private record PortfolioOrderRequest(string UserId, string AccountId, string Side, string CashAmount, string Currency);

    private record RebalancingRequest(string[] Allocations);
}
