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
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Typed client over the Upvest Investment API. Its HttpClient is wired with
/// <see cref="UpvestAuthenticationHandler"/>, so every request here is automatically authenticated
/// and signed. Request/response shapes follow the Upvest Investment API specification.
/// </summary>
public sealed class UpvestClient : IUpvestClient
{
    private readonly HttpClient _http;

    public UpvestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<UpvestUserResult> CreateUserAsync(InvestorSignup signup, CancellationToken cancellationToken)
    {
        var address = new
        {
            address_line1 = signup.Address.Line1,
            postcode = signup.Address.Postcode,
            city = signup.Address.City,
            country = signup.Address.Country
        };
        var body = new
        {
            first_name = signup.FirstName,
            last_name = signup.LastName,
            email = signup.Email,
            birth_date = signup.BirthDate,
            birth_city = signup.Address.City,
            birth_country = signup.Nationality,
            nationalities = new[] { signup.Nationality },
            phone_number = signup.PhoneNumber,
            address = address,
            postal_address = address
        };
        using var doc = await PostJsonAsync("/users", body, idempotent: true, cancellationToken);
        return new UpvestUserResult(GetString(doc.RootElement, "id"), GetString(doc.RootElement, "status"));
    }

    public async Task SubmitKycCheckAsync(string upvestUserId, InvestorSignup signup, CancellationToken cancellationToken)
    {
        var body = new
        {
            type = "KYC",
            check_confirmed_at = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            nationality = signup.Nationality,
            // Synthetic compliance evidence for the sandbox — no real personal document is referenced.
            data_download_link = $"https://eshoponweb.example/kyc/{upvestUserId}.zip",
            document_type = "ID_CARD",
            document_expiration_date = "2035-01-01",
            provider = "eShopOnWeb",
            method = "VIDEO_ID"
        };
        using var _ = await PostJsonAsync($"/users/{upvestUserId}/checks", body, idempotent: true, cancellationToken);
    }

    public async Task SetTaxResidencyAsync(string upvestUserId, InvestorSignup signup, CancellationToken cancellationToken)
    {
        var body = new
        {
            tax_residencies = new[]
            {
                new { country = signup.TaxCountry, tax_identifier_number = signup.TaxId }
            }
        };
        using var _ = await PostJsonAsync($"/users/{upvestUserId}/tax_residencies", body, idempotent: true, cancellationToken);
    }

    public async Task<UpvestUserResult> GetUserAsync(string upvestUserId, CancellationToken cancellationToken)
    {
        using var doc = await GetJsonAsync($"/users/{upvestUserId}", cancellationToken);
        return new UpvestUserResult(GetString(doc.RootElement, "id"), GetString(doc.RootElement, "status"));
    }

    public async Task<UpvestAccountGroupResult> CreateAccountGroupAsync(string upvestUserId, CancellationToken cancellationToken)
    {
        var body = new { user_id = upvestUserId, type = "PERSONAL" };
        using var doc = await PostJsonAsync("/account_groups", body, idempotent: true, cancellationToken);
        return new UpvestAccountGroupResult(GetString(doc.RootElement, "id"), GetString(doc.RootElement, "status"));
    }

    public async Task<UpvestAccountResult> CreateAccountAsync(string upvestUserId, string accountGroupId, CancellationToken cancellationToken)
    {
        var body = new { user_id = upvestUserId, account_group_id = accountGroupId, type = "TRADING", name = "Spare change" };
        using var doc = await PostJsonAsync("/accounts", body, idempotent: true, cancellationToken);
        return new UpvestAccountResult(GetString(doc.RootElement, "id"), GetString(doc.RootElement, "status"));
    }

    public async Task<UpvestAccountResult> GetAccountAsync(string accountId, CancellationToken cancellationToken)
    {
        using var doc = await GetJsonAsync($"/accounts/{accountId}", cancellationToken);
        return new UpvestAccountResult(GetString(doc.RootElement, "id"), GetString(doc.RootElement, "status"));
    }

    public async Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, string currency, CancellationToken cancellationToken)
    {
        var body = new
        {
            account_group_id = accountGroupId,
            amount = amount.ToString("0.00", CultureInfo.InvariantCulture),
            currency
        };
        using var _ = await PostJsonAsync("/virtual_cash_balances/increases", body, idempotent: true, cancellationToken);
    }

    public async Task<UpvestOrderResult> PlaceBuyOrderAsync(string upvestUserId, string accountId, string instrumentId, decimal cashAmount, string currency, CancellationToken cancellationToken)
    {
        var body = new
        {
            user_id = upvestUserId,
            account_id = accountId,
            cash_amount = cashAmount.ToString("0.00", CultureInfo.InvariantCulture),
            currency,
            side = "BUY",
            instrument_id = instrumentId,
            instrument_id_type = "ISIN",
            order_type = "MARKET",
            user_instrument_fit_acknowledgement = true
        };
        using var doc = await PostJsonAsync("/orders", body, idempotent: true, cancellationToken);
        return new UpvestOrderResult(GetString(doc.RootElement, "id"), GetString(doc.RootElement, "status"));
    }

    public async Task<UpvestOrderResult> GetOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        using var doc = await GetJsonAsync($"/orders/{orderId}", cancellationToken);
        return new UpvestOrderResult(GetString(doc.RootElement, "id"), GetString(doc.RootElement, "status"));
    }

    public async Task EnsureWebhookAsync(string callbackUrl, IEnumerable<string> eventTypes, CancellationToken cancellationToken)
    {
        // Skip if a subscription for this url already exists.
        try
        {
            using var existing = await GetJsonAsync("/webhooks", cancellationToken);
            if (ContainsWebhookUrl(existing.RootElement, callbackUrl))
            {
                return;
            }
        }
        catch
        {
            // If listing fails, fall through and attempt to create.
        }

        var body = new { title = "eShopOnWeb investing", url = callbackUrl, type = eventTypes.ToArray() };
        using var _ = await PostJsonAsync("/webhooks", body, idempotent: false, cancellationToken);
    }

    // --- helpers ------------------------------------------------------------------------

    private async Task<JsonDocument> PostJsonAsync(string path, object body, bool idempotent, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        if (idempotent)
        {
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());
        }
        return await SendAsync(request, cancellationToken);
    }

    private async Task<JsonDocument> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await SendAsync(request, cancellationToken);
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Upvest {request.Method} {request.RequestUri?.AbsolutePath} failed with status {(int)response.StatusCode}: {Truncate(payload)}");
        }
        return string.IsNullOrWhiteSpace(payload) ? JsonDocument.Parse("{}") : JsonDocument.Parse(payload);
    }

    private static bool ContainsWebhookUrl(JsonElement root, string url)
    {
        JsonElement array;
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
        }
        // Upvest paginates list results under a "data" array (with a "meta" envelope).
        else if (root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array)
        {
            array = d;
        }
        else if (root.TryGetProperty("webhooks", out var w) && w.ValueKind == JsonValueKind.Array)
        {
            array = w;
        }
        else if (root.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.Array)
        {
            array = r;
        }
        else
        {
            return false;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty("url", out var u) && string.Equals(u.GetString(), url, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static string Truncate(string value)
        => value.Length <= 300 ? value : value.Substring(0, 300);
}
