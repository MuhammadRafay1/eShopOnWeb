using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Exceptions;
using UpvestInvestmentApi.Standard.Models.Containers;
using Models = UpvestInvestmentApi.Standard.Models;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The application's single door to Upvest, implemented over the Upvest SDK. The SDK client is configured
/// once (in DI) so that every request flows through the one reusable signing handler; this class never
/// touches credentials. SDK exceptions are translated here and never leak across the application boundary.
///
/// Onboarding an investor is: create the Upvest user, report their KYC check, and record their tax
/// residency. Upvest then accepts them (the user becomes ACTIVE), after which a personal account group and
/// a trading account are provisioned to hold what is bought for them.
/// </summary>
public class UpvestGateway : IUpvestGateway
{
    private const string InstrumentIdType = "ISIN";

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestRawClient _raw;
    private readonly UpvestSettings _settings;
    private readonly IAppLogger<UpvestGateway> _logger;
    private readonly Guid _upvestClientId;

    public UpvestGateway(UpvestInvestmentApiClient client, UpvestRawClient raw, UpvestSettings settings, IAppLogger<UpvestGateway> logger)
    {
        _client = client;
        _raw = raw;
        _settings = settings;
        _logger = logger;
        _upvestClientId = Guid.Parse(settings.ClientId);
    }

    public async Task<string> RegisterInvestorAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        // 1. Create the Upvest user (the investor). Personal details are forwarded here and not kept by us.
        var userBody = new Models.UserByolCreateRequest
        {
            FirstName = details.FirstName,
            LastName = details.LastName,
            Email = details.Email,
            BirthDate = details.BirthDate.ToDateTime(TimeOnly.MinValue),
            Nationalities = new List<Models.Nationality> { ParseNationality(details.Nationality) },
            Address = BuildAddress(details.Address)
        };

        var userResponse = await RunAsync("CreateUser", () => _client.UsersApi.CreateUserAsync(
            _upvestClientId, Guid.NewGuid(),
            body: CreateUserBody.FromUserBYOLCreateRequest(userBody),
            cancellationToken: cancellationToken));

        var userId = userResponse.Data.Match(byol => byol.Id, tol => tol.Id);

        // 2. Report the shopper's completed KYC check.
        var kyc = new Models.UserCheckKnowYourCustomerCreateRequest
        {
            Type = "KYC",
            CheckConfirmedAt = DateTime.UtcNow,
            DataDownloadLink = _settings.CallbackBaseUrl.TrimEnd('/') + "/api/investing/upvest/kyc-evidence/" + userId,
            DocumentType = Models.DocumentType3.Passport,
            DocumentExpirationDate = DateTime.UtcNow.AddYears(5),
            Nationality = details.Nationality,
            ConfirmedAddress = BuildAddress(details.Address),
            KycUpdate = true,
            Provider = "eShopOnWeb",
            Method = Models.Method.ElectronicId
        };
        await RunAsync("CreateUserCheck", () => _client.UserChecksApi.CreateUserCheckAsync(
            userId, _upvestClientId,
            body: CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(kyc),
            cancellationToken: cancellationToken));

        // 3. Record the shopper's tax residency. This completes onboarding; Upvest then accepts the user.
        var taxBody = new Models.TaxResidenciesSetRequest
        {
            TaxResidencies = new List<TaxResidenciesSetRequestTaxResidencies>
            {
                TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
                    new Models.WithTaxIdentifierNumber(ParseCountry(details.TaxCountry), details.TaxId))
            }
        };
        await RunAsync("SetTaxResidencies", () => _client.TaxResidenciesApi.SetTaxResidenciesAsync(
            userId, _upvestClientId, Guid.NewGuid(), body: taxBody, cancellationToken: cancellationToken));

        _logger.LogInformation("Registered Upvest user {UserId}.", userId);
        return userId.ToString();
    }

    public async Task<UpvestEnrolment> TryCompleteEnrolmentAsync(string upvestUserId, CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(upvestUserId);
        var user = await RunAsync("RetrieveUser", () =>
            _client.UsersApi.RetrieveUserAsync(userId, _upvestClientId, cancellationToken: cancellationToken));
        var status = MapUserStatus(user.Data.Match(byol => byol.Status, tol => tol.Status));

        if (status != EnrolmentStatus.Active)
        {
            // Not accepted yet (or rejected): no holding account can exist.
            return new UpvestEnrolment(status, null, null);
        }

        // Accepted: ensure the personal account group and trading account that hold the shopper's investments.
        // These responses go through the raw client because the SDK's Account/AccountGroup models are too strict
        // for the sandbox's actual responses (see UpvestRawClient).
        var accountGroupId = await EnsureAccountGroupAsync(userId, cancellationToken);
        var accountId = await EnsureTradingAccountAsync(userId, accountGroupId, cancellationToken);
        return new UpvestEnrolment(EnrolmentStatus.Active, accountGroupId, accountId);
    }

    private async Task<string> EnsureAccountGroupAsync(Guid userId, CancellationToken cancellationToken)
    {
        var groups = await _raw.GetAsync($"/users/{userId}/account_groups", cancellationToken);
        var existing = FirstDataId(groups);
        if (existing is not null)
        {
            return existing;
        }

        var created = await _raw.PostAsync("/account_groups", new
        {
            user_id = userId,
            type = "PERSONAL",
            securities_account_number = Random.Shared.Next(100000000, 999999999).ToString(CultureInfo.InvariantCulture)
        }, cancellationToken);

        return created.GetProperty("id").GetString()!;
    }

    private async Task<string> EnsureTradingAccountAsync(Guid userId, string accountGroupId, CancellationToken cancellationToken)
    {
        var accounts = await _raw.GetAsync($"/users/{userId}/accounts", cancellationToken);
        var existing = FirstDataId(accounts, "TRADING");
        if (existing is not null)
        {
            return existing;
        }

        var created = await _raw.PostAsync("/accounts", new
        {
            user_id = userId,
            account_group_id = Guid.Parse(accountGroupId),
            type = "TRADING",
            name = "Spare change"
        }, cancellationToken);

        return created.GetProperty("id").GetString()!;
    }

    public async Task<UpvestOrderResult> PlaceInvestmentOrderAsync(string upvestUserId, string upvestAccountGroupId, string upvestAccountId, decimal amountEuros, CancellationToken cancellationToken = default)
    {
        var amount = amountEuros.ToString("F2", CultureInfo.InvariantCulture);

        // Fund the account group (sandbox virtual cash) so the buy can execute. Best-effort.
        try
        {
            await _raw.PostAsync("/virtual_cash_balances/increases", new
            {
                account_group_id = Guid.Parse(upvestAccountGroupId),
                amount,
                currency = "EUR"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Virtual cash funding before investment was not applied: {Error}.", ex.Message);
        }

        var order = await _raw.PostAsync("/orders", new
        {
            account_id = Guid.Parse(upvestAccountId),
            user_id = Guid.Parse(upvestUserId),
            side = "BUY",
            instrument_id = _settings.InstrumentId,
            instrument_id_type = InstrumentIdType,
            cash_amount = amount,
            currency = "EUR",
            order_type = "MARKET"
        }, cancellationToken);

        var orderId = order.GetProperty("id").GetString()!;
        return new UpvestOrderResult(orderId, MapOrderStatus(order.GetProperty("status").GetString()));
    }

    public async Task<InvestmentStatus> GetOrderStatusAsync(string upvestOrderId, CancellationToken cancellationToken = default)
    {
        var order = await _raw.GetAsync($"/orders/{upvestOrderId}", cancellationToken);
        return MapOrderStatus(order.TryGetProperty("status", out var s) ? s.GetString() : null);
    }

    /// <summary>The id of the first item in a <c>data</c> array (optionally filtered by a <c>type</c> value).</summary>
    private static string? FirstDataId(System.Text.Json.JsonElement root, string? type = null)
    {
        if (root.ValueKind != System.Text.Json.JsonValueKind.Object || !root.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in data.EnumerateArray())
        {
            if (type is not null && (!item.TryGetProperty("type", out var t) || t.GetString() != type))
            {
                continue;
            }

            if (item.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }

        return null;
    }

    public async Task EnsureOrderWebhookAsync(CancellationToken cancellationToken = default)
    {
        var callbackUrl = _settings.CallbackBaseUrl.TrimEnd('/') + "/api/investing/upvest/webhook";

        var existing = await RunAsync("ListWebhooks", () =>
            _client.WebhookSubscriptionsApi.ListWebhooksAsync(_upvestClientId, cancellationToken: cancellationToken));
        if (existing.Data?.Data?.Any(w => string.Equals(w.Url, callbackUrl, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return;
        }

        var body = new Models.WebhookCreateRequest
        {
            Title = "eShopOnWeb spare-change order events",
            Url = callbackUrl,
            Type = new List<Models.Type> { Models.Type.Order, Models.Type.OrderCancellation, Models.Type.Execution }
        };

        await RunAsync("CreateWebhook", () => _client.WebhookSubscriptionsApi.CreateWebhookAsync(
            _upvestClientId, body: body, cancellationToken: cancellationToken));
        _logger.LogInformation("Registered Upvest order webhook at {CallbackUrl}.", callbackUrl);
    }

    private Models.Address BuildAddress(EnrolmentAddress address) => new(
        addressLine1: address.Line1,
        postcode: address.Postcode,
        country: ParseCountry(address.Country),
        city: address.City);

    /// <summary>
    /// Runs one SDK call and translates a failure into a provider-neutral exception, logging the operation and
    /// transport status only — never the response body, which could echo personal data.
    /// </summary>
    private async Task<T> RunAsync<T>(string operation, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Upvest call {Operation} failed with status {Status}.", operation, ((ApiException)ex).ResponseCode);
            throw new UpvestGatewayException(operation, ((ApiException)ex).ResponseCode, ex);
        }
    }

    private static EnrolmentStatus MapUserStatus(Models.Status status) => status switch
    {
        Models.Status.Active => EnrolmentStatus.Active,
        Models.Status.Offboarding or Models.Status.Offboarded => EnrolmentStatus.Rejected,
        _ => EnrolmentStatus.Pending
    };

    private static InvestmentStatus MapOrderStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "FILLED" => InvestmentStatus.Settled,
        "CANCELLED" => InvestmentStatus.Failed,
        _ => InvestmentStatus.Pending
    };

    private static Models.Nationality ParseNationality(string alpha2)
    {
        // The generated enum member names are PascalCase (e.g. "De"); the alpha-2 input ("DE") matches them
        // case-insensitively.
        if (!string.IsNullOrWhiteSpace(alpha2) && Enum.TryParse<Models.Nationality>(alpha2.Trim(), ignoreCase: true, out var nationality))
        {
            return nationality;
        }

        throw new ArgumentException($"Unknown nationality '{alpha2}' (expected ISO 3166-1 alpha-2).", nameof(alpha2));
    }

    private static Models.Country ParseCountry(string alpha2)
    {
        if (!string.IsNullOrWhiteSpace(alpha2) && Enum.TryParse<Models.Country>(alpha2.Trim(), ignoreCase: true, out var country))
        {
            return country;
        }

        throw new ArgumentException($"Unknown country '{alpha2}' (expected ISO 3166-1 alpha-2).", nameof(alpha2));
    }
}
