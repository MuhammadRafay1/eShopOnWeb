using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The concrete gateway to Upvest. Every request uses the one authenticating HttpClient, so this
/// class never touches credentials or signatures — it only shapes payloads and reads responses.
/// </summary>
public sealed class UpvestClient : IUpvestClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestOptions _options;

    public UpvestClient(IHttpClientFactory httpClientFactory, IOptions<UpvestOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    private HttpClient Client => _httpClientFactory.CreateClient(UpvestTokenProvider.HttpClientName);

    public async Task<UpvestUser> CreateUserAsync(EnrolmentDetails details, CancellationToken cancellationToken)
    {
        var address = new
        {
            address_line1 = details.Address.Line1,
            postcode = details.Address.Postcode,
            city = details.Address.City,
            country = details.Address.Country,
        };

        var body = new
        {
            first_name = details.FirstName,
            last_name = details.LastName,
            email = details.Email,
            birth_date = details.BirthDate,
            nationalities = new[] { details.Nationality },
            postal_address = address,
            address,
        };

        using var response = await SendAsync(HttpMethod.Post, "users", body, idempotent: true, cancellationToken);
        var root = await ReadJsonAsync(response, "create user", cancellationToken);
        return new UpvestUser(GetString(root, "id"), GetString(root, "status"));
    }

    public async Task<UpvestUser?> GetUserAsync(string upvestUserId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"users/{upvestUserId}", null, idempotent: false, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        var root = await ReadJsonAsync(response, "get user", cancellationToken);
        return new UpvestUser(GetString(root, "id"), GetString(root, "status"));
    }

    public async Task SetTaxResidencyAsync(string upvestUserId, string taxCountry, string taxId, CancellationToken cancellationToken)
    {
        var body = new
        {
            tax_residencies = new[]
            {
                new { country = taxCountry, tax_identifier_number = taxId }
            }
        };

        using var response = await SendAsync(HttpMethod.Post, $"users/{upvestUserId}/tax_residencies", body, idempotent: true, cancellationToken);
        await EnsureSuccessAsync(response, "set tax residency", cancellationToken);
    }

    public async Task CreateKycCheckAsync(string upvestUserId, string nationality, CancellationToken cancellationToken)
    {
        // A standard passed KYC check. Carries no personal data beyond the nationality already held by Upvest.
        var body = new
        {
            type = "KYC",
            check_confirmed_at = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            data_download_link = $"https://eshoponweb.example/kyc/{Guid.NewGuid()}.zip",
            document_type = "ID_CARD",
            nationality,
            provider = "eShopOnWeb KYC",
            method = "VIDEO_ID",
        };
        using var response = await SendAsync(HttpMethod.Post, $"users/{upvestUserId}/checks", body, idempotent: true, cancellationToken);
        await EnsureSuccessAsync(response, "create KYC check", cancellationToken);
    }

    public async Task<string> CreateAccountGroupAsync(string upvestUserId, CancellationToken cancellationToken)
    {
        var body = new { user_id = upvestUserId, type = "PERSONAL" };
        using var response = await SendAsync(HttpMethod.Post, "account_groups", body, idempotent: true, cancellationToken);
        var root = await ReadJsonAsync(response, "create account group", cancellationToken);
        return GetString(root, "id");
    }

    public async Task<string> CreateAccountAsync(string upvestUserId, string accountGroupId, CancellationToken cancellationToken)
    {
        var body = new
        {
            user_id = upvestUserId,
            account_group_id = accountGroupId,
            type = "TRADING",
            name = "eShopOnWeb spare change",
        };
        using var response = await SendAsync(HttpMethod.Post, "accounts", body, idempotent: true, cancellationToken);
        var root = await ReadJsonAsync(response, "create account", cancellationToken);
        return GetString(root, "id");
    }

    public async Task IncreaseVirtualCashAsync(string accountGroupId, decimal amountEuros, CancellationToken cancellationToken)
    {
        var body = new
        {
            account_group_id = accountGroupId,
            amount = FormatAmount(amountEuros),
            currency = "EUR",
        };
        using var response = await SendAsync(HttpMethod.Post, "virtual_cash_balances/increases", body, idempotent: true, cancellationToken);
        await EnsureSuccessAsync(response, "increase virtual cash", cancellationToken);
    }

    public async Task<UpvestOrder> PlaceOrderAsync(string upvestUserId, string accountId, decimal amountEuros, CancellationToken cancellationToken)
    {
        var body = new
        {
            user_id = upvestUserId,
            account_id = accountId,
            cash_amount = FormatAmount(amountEuros),
            currency = "EUR",
            side = "BUY",
            instrument_id = _options.InstrumentId,
            instrument_id_type = "ISIN",
            order_type = "MARKET",
            user_instrument_fit_acknowledgement = true,
        };
        using var response = await SendAsync(HttpMethod.Post, "orders", body, idempotent: true, cancellationToken);
        var root = await ReadJsonAsync(response, "place order", cancellationToken);
        return new UpvestOrder(GetString(root, "id"), GetString(root, "status"));
    }

    public async Task<UpvestOrder?> GetOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"orders/{orderId}", null, idempotent: false, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        var root = await ReadJsonAsync(response, "get order", cancellationToken);
        return new UpvestOrder(GetString(root, "id"), GetString(root, "status"));
    }

    public async Task EnsureWebhookAsync(string callbackUrl, CancellationToken cancellationToken)
    {
        // Already subscribed?
        using (var list = await SendAsync(HttpMethod.Get, "webhooks", null, idempotent: false, cancellationToken))
        {
            if (list.IsSuccessStatusCode)
            {
                var root = await ReadJsonAsync(list, "list webhooks", cancellationToken);
                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("url", out var url) && url.GetString() == callbackUrl)
                            return;
                    }
                }
            }
        }

        var body = new
        {
            title = "eShopOnWeb investing events",
            url = callbackUrl,
            type = new[] { "USER", "ORDER" },
        };
        using var response = await SendAsync(HttpMethod.Post, "webhooks", body, idempotent: true, cancellationToken);
        await EnsureSuccessAsync(response, "create webhook", cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, bool idempotent, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            // Buffer into a byte array so Content-Length is always set (JsonContent streams without it),
            // and set an explicit application/json content-type (no charset) so the signed value matches the wire.
            var json = JsonSerializer.SerializeToUtf8Bytes(body);
            var content = new ByteArrayContent(json);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            request.Content = content;
        }

        if (idempotent)
            request.Headers.TryAddWithoutValidation("idempotency-key", Guid.NewGuid().ToString());

        return await Client.SendAsync(request, cancellationToken);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, operation, cancellationToken);
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return doc.RootElement.Clone();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Read only the error "type" from problem+json; never surface bodies that may carry personal data.
        string? errorType = null;
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (doc.RootElement.TryGetProperty("type", out var type))
                errorType = type.GetString();
        }
        catch
        {
            // Non-JSON or empty error body: leave errorType null.
        }

        throw new UpvestApiException(response.StatusCode, operation, errorType);
    }

    private static string FormatAmount(decimal amountEuros) =>
        amountEuros.ToString("0.00", CultureInfo.InvariantCulture);

    private static string GetString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
