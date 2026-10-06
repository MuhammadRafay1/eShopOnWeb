using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Talks to the Upvest Investment API. The only type that knows Upvest's wire format. Every
/// request is issued on the shared "Upvest" HttpClient whose pipeline carries the single
/// authenticating handler, so authentication is never set up here.
/// </summary>
public sealed class UpvestGateway : IUpvestGateway
{
    private const string Currency = "EUR";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;

    public UpvestGateway(IHttpClientFactory httpClientFactory, IOptions<UpvestSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    private HttpClient Client => _httpClientFactory.CreateClient(UpvestHttpClient.Name);

    public async Task<UpvestEnrolmentResult> EnrolInvestorAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        // 1. Create the Upvest user (the investor) from the shop's sign-up form.
        var userPayload = new
        {
            first_name = details.FirstName,
            last_name = details.LastName,
            email = details.Email,
            birth_date = details.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            nationalities = new[] { details.Nationality },
            phone_number = details.PhoneNumber,
            address = new
            {
                address_line1 = details.Address.Line1,
                postcode = details.Address.Postcode,
                city = details.Address.City,
                country = details.Address.Country,
            },
            fatca = new
            {
                status = false,
                confirmed_at = DateTimeOffset.UtcNow.ToString("o"),
            },
        };
        using var userDoc = await PostAsync("users", userPayload, "create user", cancellationToken);
        var userId = GetString(userDoc, "id");

        // 2. Record the shopper's tax residency.
        var taxPayload = new
        {
            tax_residencies = new[]
            {
                new { country = details.TaxCountry, tax_identifier_number = details.TaxId },
            },
        };
        using (await PostAsync($"users/{userId}/tax_residencies", taxPayload, "set tax residency", cancellationToken)) { }

        // 3. Submit the KYC check whose outcome decides whether Upvest accepts the investor.
        var confirmedAddress = new
        {
            address_line1 = details.Address.Line1,
            postcode = details.Address.Postcode,
            city = details.Address.City,
            country = details.Address.Country,
        };
        var checkPayload = new
        {
            type = "KYC",
            check_confirmed_at = DateTimeOffset.UtcNow.ToString("o"),
            data_download_link = "https://eshoponweb.example/kyc/" + userId,
            document_type = "ID_CARD",
            document_expiration_date = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            nationality = details.Nationality,
            provider = "eShopOnWeb KYC",
            method = "VIDEO_ID",
            confirmed_address = confirmedAddress,
        };
        using var checkDoc = await PostAsync($"users/{userId}/checks", checkPayload, "create KYC check", cancellationToken);
        var checkId = GetString(checkDoc, "id");

        // The account that holds the investments can only be created once Upvest has accepted the
        // user, so it is provisioned later by the reconciler (see ProvisionAccountAsync).
        return new UpvestEnrolmentResult(userId, string.Empty, string.Empty, checkId);
    }

    public async Task<UpvestAccountResult> ProvisionAccountAsync(string upvestUserId, CancellationToken cancellationToken = default)
    {
        var (accountGroupId, accountId) = await TryProvisionAccountAsync(upvestUserId, cancellationToken);
        return new UpvestAccountResult(accountGroupId, accountId);
    }

    private async Task<(string AccountGroupId, string AccountId)> TryProvisionAccountAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            // An account group and account can only be created once Upvest has accepted the user.
            // Reuse any that already exist so provisioning is safe to retry.
            var accountGroupId = await GetFirstIdAsync($"users/{userId}/account_groups", cancellationToken);
            if (string.IsNullOrEmpty(accountGroupId))
            {
                using var groupDoc = await PostAsync("account_groups", new { user_id = userId, type = "PERSONAL" }, "create account group", cancellationToken);
                accountGroupId = GetString(groupDoc, "id");
            }

            var (accountId, accountStatus) = await GetFirstAccountAsync($"users/{userId}/accounts", cancellationToken);
            if (string.IsNullOrEmpty(accountId) && !string.IsNullOrEmpty(accountGroupId))
            {
                using var accountDoc = await PostAsync("accounts",
                    new { user_id = userId, account_group_id = accountGroupId, type = "TRADING", name = "Spare change" }, "create account", cancellationToken);
                accountId = GetString(accountDoc, "id");
                accountStatus = GetString(accountDoc, "status");
            }

            // The account is only usable for orders once it has become ACTIVE (it is initially
            // PENDING_APPROVAL). Report provisioned only then; otherwise retry on a later tick.
            if (!string.IsNullOrEmpty(accountId) && accountStatus == "ACTIVE")
            {
                return (accountGroupId, accountId);
            }

            return (string.Empty, string.Empty);
        }
        catch (UpvestApiException)
        {
            // Not provisionable yet (e.g. the user is not accepted): retried on a later tick.
            return (string.Empty, string.Empty);
        }
    }

    private async Task<string> GetFirstIdAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await GetAsync(path, "list account groups", cancellationToken);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var id)) return id.GetString() ?? string.Empty;
                }
            }
        }
        catch (UpvestApiException)
        {
            // Listing not available; fall back to creation.
        }
        return string.Empty;
    }

    private async Task<(string Id, string Status)> GetFirstAccountAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = await GetAsync(path, "list accounts", cancellationToken);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var i) ? i.GetString() ?? string.Empty : string.Empty;
                    var status = item.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
                    if (!string.IsNullOrEmpty(id)) return (id, status);
                }
            }
        }
        catch (UpvestApiException)
        {
            // Listing not available; fall back to creation.
        }
        return (string.Empty, string.Empty);
    }

    public async Task<EnrolmentStatus> GetEnrolmentStatusAsync(string upvestUserId, string checkId, CancellationToken cancellationToken = default)
    {
        // Upvest exposes checks as a list; find the one we created and read its status.
        using var doc = await GetAsync($"users/{upvestUserId}/checks", "list KYC checks", cancellationToken);
        string? status = null;
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var check in data.EnumerateArray())
            {
                if (check.TryGetProperty("id", out var id) && id.GetString() == checkId)
                {
                    status = check.TryGetProperty("status", out var s) ? s.GetString() : null;
                    break;
                }
            }
        }
        return status switch
        {
            "PASSED" => EnrolmentStatus.Active,
            "FAILED" => EnrolmentStatus.Rejected,
            _ => EnrolmentStatus.Pending,
        };
    }

    public async Task<UpvestInvestmentResult> InvestAsync(string accountGroupId, string accountId, decimal amount, CancellationToken cancellationToken = default)
    {
        var cashAmount = amount.ToString("0.00", CultureInfo.InvariantCulture);

        // Fund the account with exactly the cash to be invested, then buy the fund with it.
        var fundPayload = new { account_group_id = accountGroupId, amount = cashAmount, currency = Currency };
        using (await PostAsync("virtual_cash_balances/increases", fundPayload, "fund account", cancellationToken)) { }

        // The cash increase settles asynchronously; give it a moment to be reflected on the
        // account group's balance before placing the order, so the order is not rejected for
        // insufficient funds.
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);

        var orderPayload = new
        {
            account_id = accountId,
            side = "BUY",
            instrument_id = _settings.InstrumentId,
            instrument_id_type = "ISIN",
            cash_amount = cashAmount,
            currency = Currency,
            order_type = "MARKET",
            user_instrument_fit_acknowledgement = true,
        };
        using var orderDoc = await PostAsync("orders", orderPayload, "place order", cancellationToken);
        return new UpvestInvestmentResult(GetString(orderDoc, "id"), MapOrderStatus(GetString(orderDoc, "status")));
    }

    public async Task<InvestmentStatus> GetInvestmentStatusAsync(string upvestOrderId, CancellationToken cancellationToken = default)
    {
        using var doc = await GetAsync($"orders/{upvestOrderId}", "get order", cancellationToken);
        return MapOrderStatus(GetString(doc, "status"));
    }

    private static InvestmentStatus MapOrderStatus(string? status) => status switch
    {
        "FILLED" => InvestmentStatus.Settled,
        "CANCELLED" => InvestmentStatus.Failed,
        _ => InvestmentStatus.Pending,
    };

    private async Task<JsonDocument> PostAsync(string path, object payload, string operation, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
        return await SendAsync(request, operation, cancellationToken);
    }

    private async Task<JsonDocument> GetAsync(string path, string operation, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await SendAsync(request, operation, cancellationToken);
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        using var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string? requestId = response.Headers.TryGetValues("upvest-request-id", out var ids)
                ? string.Join(",", ids) : null;
            throw new UpvestApiException(response.StatusCode, operation, requestId);
        }

        return string.IsNullOrWhiteSpace(body) ? JsonDocument.Parse("{}") : JsonDocument.Parse(body);
    }

    private static string GetString(JsonDocument doc, string property) =>
        doc.RootElement.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
