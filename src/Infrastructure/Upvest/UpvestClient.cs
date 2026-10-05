using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Typed client over the Upvest Investment API. Authentication (OAuth token +
/// HTTP signature) is handled entirely by <see cref="UpvestAuthenticationHandler"/>
/// on the underlying <see cref="HttpClient"/>; this class only shapes requests
/// and reads responses.
/// </summary>
public sealed class UpvestClient : IUpvestClient
{
    // Documented Upvest sandbox consent document ids (not secrets).
    private const string TermsConsentDocumentId = "d0b83880-3809-4eee-b0df-ca50db84c15a";
    private const string DataPrivacyConsentDocumentId = "7ab0fc5c-8157-4acd-b02d-6ccda0e81dec";

    private readonly HttpClient _http;
    private readonly UpvestOptions _options;
    private readonly ILogger<UpvestClient> _logger;

    public UpvestClient(HttpClient http, IOptions<UpvestOptions> options, ILogger<UpvestClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    private static string Rfc3339Now() => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<JsonElement> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // Upvest error bodies carry an error type/detail, not shopper PII.
            throw new HttpRequestException(
                $"Upvest {request.Method} {request.RequestUri?.AbsolutePath} failed: {(int)response.StatusCode} {Summarise(payload)}");
        }

        return payload.Length == 0 ? default : JsonDocument.Parse(payload).RootElement.Clone();
    }

    private static string Summarise(string payload)
    {
        try
        {
            var root = JsonDocument.Parse(payload).RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
            return $"{type} {detail}".Trim();
        }
        catch { return string.Empty; }
    }

    private static string Field(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    public async Task<UpvestUserRef> CreateUserAsync(EnrolmentDetails d, CancellationToken ct)
    {
        var now = Rfc3339Now();
        var body = new
        {
            first_name = d.FirstName,
            last_name = d.LastName,
            email = d.Email,
            birth_date = d.BirthDate,
            // The shop form does not collect birth city/country; derive sensible
            // values (Upvest requires both) from the data it does collect.
            birth_city = d.City,
            birth_country = d.Nationality,
            nationalities = new[] { d.Nationality },
            phone_number = d.PhoneNumber,
            address = new { address_line1 = d.AddressLine1, postcode = d.Postcode, city = d.City, country = d.Country },
            fatca = new { status = false, confirmed_at = now },
            terms_and_conditions = new { consent_document_id = TermsConsentDocumentId, confirmed_at = now },
            data_privacy_and_sharing_agreement = new { consent_document_id = DataPrivacyConsentDocumentId, confirmed_at = now },
        };
        var el = await SendAsync(new HttpRequestMessage(HttpMethod.Post, "/users") { Content = Json(body) }, ct);
        return new UpvestUserRef(Field(el, "id"), Field(el, "status"));
    }

    public async Task SubmitKycCheckAsync(string upvestUserId, EnrolmentDetails d, CancellationToken ct)
    {
        var body = new
        {
            type = "KYC",
            check_confirmed_at = Rfc3339Now(),
            data_download_link = $"https://evidence.eshoponweb.example/kyc/{upvestUserId}.zip",
            document_type = "ID_CARD",
            provider = "eShopOnWeb",
            method = "VIDEO_ID",
            // Supplying the confirmed address satisfies the POR requirement.
            confirmed_address = new { address_line1 = d.AddressLine1, postcode = d.Postcode, city = d.City, country = d.Country },
        };
        await SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/users/{upvestUserId}/checks") { Content = Json(body) }, ct);
    }

    public async Task SubmitInstrumentFitCheckAsync(string upvestUserId, CancellationToken ct)
    {
        // Best-effort: the shopper activates on KYC alone and orders carry an
        // instrument-fit acknowledgement, so a rejection here must not block enrolment.
        try
        {
            var body = new
            {
                type = "INSTRUMENT_FIT",
                check_confirmed_at = Rfc3339Now(),
                instrument_suitability = new { suitability = true },
            };
            await SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/users/{upvestUserId}/checks") { Content = Json(body) }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Upvest INSTRUMENT_FIT check was not accepted (continuing): {Reason}", ex.Message);
        }
    }

    public async Task SetTaxResidenciesAsync(string upvestUserId, string taxCountry, string? taxId, CancellationToken ct)
    {
        object residency = string.IsNullOrWhiteSpace(taxId)
            ? new { country = taxCountry, missing_tin_reason = "OTHER_REASONS" }
            : new { country = taxCountry, tax_identifier_number = taxId };
        var body = new { tax_residencies = new[] { residency } };
        await SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/users/{upvestUserId}/tax_residencies") { Content = Json(body) }, ct);
    }

    public async Task<UpvestUserRef> GetUserAsync(string upvestUserId, CancellationToken ct)
    {
        var el = await SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/users/{upvestUserId}"), ct);
        return new UpvestUserRef(Field(el, "id"), Field(el, "status"));
    }

    public async Task<UpvestAccountGroupRef> CreateAccountGroupAsync(string upvestUserId, CancellationToken ct)
    {
        var body = new { user_id = upvestUserId, type = "PERSONAL" };
        var el = await SendAsync(new HttpRequestMessage(HttpMethod.Post, "/account_groups") { Content = Json(body) }, ct);
        return new UpvestAccountGroupRef(Field(el, "id"), Field(el, "status"));
    }

    public async Task<UpvestAccountRef> CreateAccountAsync(string upvestUserId, string accountGroupId, CancellationToken ct)
    {
        var body = new { user_id = upvestUserId, account_group_id = accountGroupId, type = "TRADING" };
        var el = await SendAsync(new HttpRequestMessage(HttpMethod.Post, "/accounts") { Content = Json(body) }, ct);
        return new UpvestAccountRef(Field(el, "id"), Field(el, "status"));
    }

    public async Task<UpvestAccountRef> GetAccountAsync(string accountId, CancellationToken ct)
    {
        var el = await SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/accounts/{accountId}"), ct);
        return new UpvestAccountRef(Field(el, "id"), Field(el, "status"));
    }

    public async Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, CancellationToken ct)
    {
        var body = new { account_group_id = accountGroupId, amount = Money(amount), currency = "EUR" };
        await SendAsync(new HttpRequestMessage(HttpMethod.Post, "/virtual_cash_balances/increases") { Content = Json(body) }, ct);
    }

    public async Task<UpvestOrderRef> PlaceBuyOrderAsync(string upvestUserId, string accountId, decimal cashAmount, CancellationToken ct)
    {
        var body = new
        {
            user_id = upvestUserId,
            account_id = accountId,
            cash_amount = Money(cashAmount),
            currency = "EUR",
            side = "BUY",
            instrument_id = _options.InstrumentId,
            instrument_id_type = "ISIN",
            order_type = "MARKET",
            user_instrument_fit_acknowledgement = true,
        };
        var el = await SendAsync(new HttpRequestMessage(HttpMethod.Post, "/orders") { Content = Json(body) }, ct);
        return new UpvestOrderRef(Field(el, "id"), Field(el, "status"));
    }

    public async Task<UpvestOrderRef> GetOrderAsync(string orderId, CancellationToken ct)
    {
        var el = await SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/orders/{orderId}"), ct);
        return new UpvestOrderRef(Field(el, "id"), Field(el, "status"));
    }

    public async Task<string?> CreateEnabledWebhookAsync(string callbackUrl, CancellationToken ct)
    {
        // Best-effort: webhooks accelerate reconciliation but are not required for it.
        try
        {
            var types = new[] { "USER", "USER_CHECK", "ACCOUNT", "ACCOUNT_GROUP", "ORDER", "EXECUTION" };
            var created = await SendAsync(new HttpRequestMessage(HttpMethod.Post, "/webhooks")
            {
                Content = Json(new { title = "eShopOnWeb investing", url = callbackUrl, type = types }),
            }, ct);
            var id = Field(created, "id");
            if (string.IsNullOrEmpty(id)) return null;

            await SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/webhooks/{id}")
            {
                Content = Json(new { enabled = true, title = "eShopOnWeb investing", url = callbackUrl, type = types }),
            }, ct);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Upvest webhook registration skipped (continuing): {Reason}", ex.Message);
            return null;
        }
    }
}
