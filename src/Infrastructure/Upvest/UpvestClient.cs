using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Typed client for the Upvest Investment API. All authentication (OAuth bearer + HTTP message
/// signature) is handled by the <see cref="UpvestSigningHandler"/> on the underlying HttpClient.
/// </summary>
public class UpvestClient : IUpvestClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

    private readonly HttpClient _httpClient;
    private readonly UpvestSettings _settings;

    public UpvestClient(HttpClient httpClient, IOptions<UpvestSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<UpvestUser> CreateUserAsync(InvestorSignUp form, CancellationToken cancellationToken = default)
    {
        // Personal details are forwarded to Upvest here and never persisted or logged by this app.
        var body = new
        {
            first_name = form.FirstName,
            last_name = form.LastName,
            email = form.Email,
            birth_date = form.BirthDate,
            birth_city = form.Address.City,
            birth_country = form.Nationality,
            nationalities = new[] { form.Nationality },
            address = ToUpvestAddress(form),
            phone_number = form.PhoneNumber
        };
        using var doc = await SendAsync(HttpMethod.Post, "/users", body, idempotent: true, cancellationToken);
        return ReadIdStatus<UpvestUser>(doc!, (id, status) => new UpvestUser(id, status));
    }

    public async Task CreateTaxResidencyAsync(string userId, string taxCountry, string taxId, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            tax_residencies = new[] { new { country = taxCountry, tax_identifier_number = taxId } }
        };
        (await SendAsync(HttpMethod.Post, $"/users/{userId}/tax_residencies", body, idempotent: true, cancellationToken))?.Dispose();
    }

    public async Task CreateKycCheckAsync(string userId, InvestorSignUp form, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            type = "KYC",
            check_confirmed_at = Now(),
            data_download_link = "https://eshoponweb.example/kyc-evidence",
            document_type = "ID_CARD",
            document_expiration_date = DateTime.UtcNow.AddYears(5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            nationality = form.Nationality,
            provider = "eShopOnWeb",
            method = "VIDEO_ID",
            confirmed_address = ToUpvestAddress(form)
        };
        (await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", body, idempotent: true, cancellationToken))?.Dispose();
    }

    public async Task CreateProofOfResidencyCheckAsync(string userId, InvestorSignUp form, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            type = "POR",
            check_confirmed_at = Now(),
            issuance_date = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            data_download_link = "https://eshoponweb.example/por-evidence",
            document_type = "UTILITY_BILL",
            provider = "eShopOnWeb",
            method = "VIDEO_ID",
            confirmed_address = ToUpvestAddress(form)
        };
        (await SendAsync(HttpMethod.Post, $"/users/{userId}/checks", body, idempotent: true, cancellationToken))?.Dispose();
    }

    public async Task<UpvestUser> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/users/{userId}", body: null, idempotent: false, cancellationToken);
        return ReadIdStatus<UpvestUser>(doc!, (id, status) => new UpvestUser(id, status));
    }

    public async Task<IReadOnlyList<string>> GetCheckStatusesAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/users/{userId}/checks", body: null, idempotent: false, cancellationToken);
        var statuses = new List<string>();
        if (doc!.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("status", out var s) && s.GetString() is { } status)
                    statuses.Add(status);
            }
        }
        return statuses;
    }

    public async Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken cancellationToken = default)
    {
        var body = new { user_id = userId, type = "PERSONAL" };
        using var doc = await SendAsync(HttpMethod.Post, "/account_groups", body, idempotent: true, cancellationToken);
        return ReadIdStatus<UpvestAccountGroup>(doc!, (id, status) => new UpvestAccountGroup(id, status));
    }

    public async Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, CancellationToken cancellationToken = default)
    {
        var body = new { user_id = userId, account_group_id = accountGroupId, type = "TRADING", name = "Spare change" };
        using var doc = await SendAsync(HttpMethod.Post, "/accounts", body, idempotent: true, cancellationToken);
        return ReadIdStatus<UpvestAccount>(doc!, (id, status) => new UpvestAccount(id, status));
    }

    public async Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/accounts/{accountId}", body: null, idempotent: false, cancellationToken);
        return ReadIdStatus<UpvestAccount>(doc!, (id, status) => new UpvestAccount(id, status));
    }

    public async Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, CancellationToken cancellationToken = default)
    {
        var body = new { account_group_id = accountGroupId, amount = Money(amount), currency = "EUR" };
        (await SendAsync(HttpMethod.Post, "/virtual_cash_balances/increases", body, idempotent: true, cancellationToken))?.Dispose();
    }

    public async Task<UpvestOrder> PlaceBuyOrderAsync(string userId, string accountId, decimal cashAmount, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            user_id = userId,
            account_id = accountId,
            cash_amount = Money(cashAmount),
            currency = "EUR",
            side = "BUY",
            instrument_id = _settings.InstrumentId,
            instrument_id_type = "ISIN",
            order_type = "MARKET",
            user_instrument_fit_acknowledgement = true
        };
        using var doc = await SendAsync(HttpMethod.Post, "/orders", body, idempotent: true, cancellationToken);
        return ReadIdStatus<UpvestOrder>(doc!, (id, status) => new UpvestOrder(id, status));
    }

    public async Task<UpvestOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, $"/orders/{orderId}", body: null, idempotent: false, cancellationToken);
        return ReadIdStatus<UpvestOrder>(doc!, (id, status) => new UpvestOrder(id, status));
    }

    public async Task EnsureWebhookAsync(string callbackUrl, CancellationToken cancellationToken = default)
    {
        var body = new { title = "eShopOnWeb investing", url = callbackUrl, type = new[] { "ALL" } };
        // Best-effort: a conflict means a matching webhook already exists.
        using var response = await SendRawAsync(HttpMethod.Post, "/webhooks", body, idempotent: false, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Conflict)
        {
            throw await UpvestApiException.FromResponseAsync(response, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<UpvestJwk>> GetVerificationKeysAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await SendAsync(HttpMethod.Get, "/auth/verify_keys", body: null, idempotent: false, cancellationToken);
        var keys = new List<UpvestJwk>();
        if (doc!.RootElement.TryGetProperty("keys", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var k in arr.EnumerateArray())
            {
                keys.Add(new UpvestJwk(
                    k.GetProperty("kid").GetString() ?? string.Empty,
                    k.GetProperty("x").GetString() ?? string.Empty,
                    k.GetProperty("y").GetString() ?? string.Empty));
            }
        }
        return keys;
    }

    private static object ToUpvestAddress(InvestorSignUp form) => new
    {
        address_line1 = form.Address.Line1,
        postcode = form.Address.Postcode,
        city = form.Address.City,
        country = form.Address.Country
    };

    private async Task<JsonDocument?> SendAsync(HttpMethod method, string path, object? body, bool idempotent, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(method, path, body, idempotent, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await UpvestApiException.FromResponseAsync(response, cancellationToken);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (stream)
        {
            if (stream.CanSeek && stream.Length == 0) return null;
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body, bool idempotent, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        if (idempotent)
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        if (body is not null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(body, SerializerOptions);
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.TryAddWithoutValidation("content-type", "application/json");
        }
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static T ReadIdStatus<T>(JsonDocument doc, Func<string, string, T> factory)
    {
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetString() ?? string.Empty;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
        return factory(id, status);
    }

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
