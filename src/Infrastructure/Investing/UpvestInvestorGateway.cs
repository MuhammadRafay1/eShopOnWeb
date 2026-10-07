using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Exceptions;
using UpvestInvestmentApi.Standard.Http.Response;
using Containers = UpvestInvestmentApi.Standard.Models.Containers;
using Models = UpvestInvestmentApi.Standard.Models;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// The sole implementation of <see cref="IUpvestInvestorGateway"/>: it is the only type that uses the
/// Upvest SDK. It translates the shop's investor concepts into the Upvest TOL onboarding and trading
/// flows. Credentials and signing are handled centrally by the shared authentication handler the SDK
/// client is wired with; nothing here attaches credentials.
/// </summary>
public class UpvestInvestorGateway : IUpvestInvestorGateway
{
    // Sandbox consent-document identifiers used by the Upvest onboarding workflow package.
    private static readonly Guid TermsConsentDocumentId = Guid.Parse("d0b83880-3809-4eee-b0df-ca50db84c15a");
    private static readonly Guid DataPrivacyConsentDocumentId = Guid.Parse("7ab0fc5c-8157-4acd-b02d-6ccda0e81dec");

    private const string WebhookCallbackPath = "/api/investing/upvest-webhook";

    // Ensure the webhook subscription is attempted at most once per process (best effort).
    private static int _webhookEnsured;

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestSettings _settings;
    private readonly UpvestRawResponseCallback _rawResponse;
    private readonly IAppLogger<UpvestInvestorGateway> _logger;
    private readonly Guid _clientId;

    public UpvestInvestorGateway(
        UpvestInvestmentApiClient client,
        UpvestRawResponseCallback rawResponse,
        IOptions<UpvestSettings> settings,
        IAppLogger<UpvestInvestorGateway> logger)
    {
        _client = client;
        _rawResponse = rawResponse;
        _settings = settings.Value;
        _logger = logger;
        _clientId = Guid.Parse(_settings.ClientId);
    }

    public async Task<UpvestEnrolmentResult> EnrolInvestorAsync(InvestorSignUpForm form, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var address = new Models.Address(form.Address.Line1, form.Address.Postcode, ParseCountry(form.Address.Country), form.Address.City);

        // 1. Create the user (Take-Our-License variant).
        var userBody = Containers.CreateUserBody.FromUserTOLCreateRequest(
            new Models.UserTolCreateRequest(
                form.FirstName,
                form.LastName,
                form.Email,
                form.BirthDate.Date,
                new List<Models.Nationality> { ParseNationality(form.Nationality) },
                address,
                new Models.Fatca(false, now))
            {
                PhoneNumber = form.PhoneNumber,
                BirthCity = form.Address.City,
                BirthCountry = ParseBirthCountry(form.Nationality),
                TermsAndConditions = new Models.TermsAndConditions(TermsConsentDocumentId, now),
                DataPrivacyAndSharingAgreement = new Models.DataPrivacyAndSharingAgreement(DataPrivacyConsentDocumentId, now),
            });

        var userResponse = await Step("create-user", () => _client.UsersApi.CreateUserAsync(
            _clientId, Guid.NewGuid(), body: userBody, cancellationToken: cancellationToken));
        var userId = userResponse.Match(byol => byol.Id, tol => tol.Id);

        // 2. Submit the KYC check. Providing the confirmed address also satisfies proof-of-residence.
        var kycBody = Containers.CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(
            new Models.UserCheckKnowYourCustomerCreateRequest(
                "KYC",
                now,
                "https://eshoponweb.example/kyc-evidence",
                Models.DocumentType3.Passport,
                "eShopOnWeb",
                Models.Method.VideoId)
            {
                Nationality = form.Nationality,
                ConfirmedAddress = address,
            });

        var checkResponse = await Step("create-user-check", () => _client.UserChecksApi.CreateUserCheckAsync(
            userId, _clientId, body: kycBody, cancellationToken: cancellationToken));
        var kycCheckId = checkResponse.Id;

        // 3. Declare tax residency.
        var taxCountry = ParseCountry(form.TaxCountry);
        var taxResidency = string.IsNullOrWhiteSpace(form.TaxId)
            ? Containers.TaxResidenciesSetRequestTaxResidencies.FromWithoutTaxIdentifierNumber(
                new Models.WithoutTaxIdentifierNumber(taxCountry, Models.MissingTinReason.OtherReasons))
            : Containers.TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
                new Models.WithTaxIdentifierNumber(taxCountry, form.TaxId));
        var taxBody = new Models.TaxResidenciesSetRequest(
            new List<Containers.TaxResidenciesSetRequestTaxResidencies> { taxResidency });

        await Step("set-tax-residencies", () => _client.TaxResidenciesApi.SetTaxResidenciesAsync(
            userId, _clientId, Guid.NewGuid(), body: taxBody, cancellationToken: cancellationToken));

        // The account group and trading account cannot be created until Upvest activates the user, so
        // they are provisioned later (see ProvisionAccountsAsync), driven by reconciliation.
        return new UpvestEnrolmentResult(userId, kycCheckId);
    }

    public async Task<UpvestAccountRefs> ProvisionAccountsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var accountGroupId = await GetOrCreateAccountGroupAsync(userId, cancellationToken);
        var accountId = await GetOrCreateTradingAccountAsync(userId, accountGroupId, cancellationToken);
        return new UpvestAccountRefs(accountGroupId, accountId);
    }

    public async Task<EnrolmentStatus> GetEnrolmentStatusAsync(Guid userId, Guid kycCheckId, CancellationToken cancellationToken)
    {
        var userResponse = await _client.UsersApi.RetrieveUserAsync(userId, _clientId, cancellationToken: cancellationToken);
        var userStatus = userResponse.Data.Match(byol => byol.Status, tol => tol.Status);
        if (userStatus == Models.Status.Active)
        {
            return EnrolmentStatus.Active;
        }

        // The user stays INACTIVE on a KYC rejection; the failure shows on the check itself.
        try
        {
            var checkResponse = await _client.UserChecksApi.RetrieveUserCheckAsync(
                userId, kycCheckId, _clientId, cancellationToken: cancellationToken);
            var kycStatus = checkResponse.Data.MatchSome(
                userCheckKnowYourCustomer: kyc => (Models.Status6?)kyc.Status);
            if (kycStatus == Models.Status6.Failed)
            {
                return EnrolmentStatus.Rejected;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not read KYC check status for an enrolment: {Error}", ex.Message);
        }

        return EnrolmentStatus.Pending;
    }

    public async Task<Guid> InvestAsync(Guid accountGroupId, Guid accountId, Guid userId, decimal amountEur, CancellationToken cancellationToken)
    {
        var amount = amountEur.ToString("F2", CultureInfo.InvariantCulture);

        // Fund the account group with the cash to be invested so the buy order can fill (sandbox virtual cash).
        var fundingBody = new Models.VirtualCashBalanceVirtualCashIncreaseCreateRequest(
            accountGroupId, amount, Models.Currency1.Eur);
        await CallRawAsync("virtual-cash-increase", () => _client.VirtualCashBalancesApi.CreateVirtualCashIncreaseAsync(
            _clientId, Guid.NewGuid(), body: fundingBody, cancellationToken: cancellationToken));

        // Place a single nominal market buy order for the whole cash amount of the configured fund.
        var orderBody = new Models.OrderPlaceRequest(accountId, Models.Side.Buy, _settings.InstrumentId, "ISIN")
        {
            UserId = userId,
            CashAmount = amount,
            Currency = Models.Currency29.Eur,
            OrderType = Models.OrderType.Market,
            UserInstrumentFitAcknowledgement = true,
        };

        var orderBody2 = await CallRawAsync("place-order", () => _client.OrdersApi.PlaceOrderAsync(
            _clientId, Guid.NewGuid(), body: orderBody, cancellationToken: cancellationToken));
        return ReadId(orderBody2) ?? throw new InvalidOperationException("Could not determine the Upvest order id.");
    }

    public async Task<InvestmentStatus> GetInvestmentStatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var body = await CallRawAsync("retrieve-order", () =>
            _client.OrdersApi.RetrieveOrderAsync(orderId, _clientId, cancellationToken: cancellationToken));
        return ReadStatus(body) switch
        {
            "FILLED" => InvestmentStatus.Settled,
            "CANCELLED" => InvestmentStatus.Failed,
            _ => InvestmentStatus.Pending,
        };
    }

    private static string? ReadStatus(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return (JToken.Parse(body) as JObject)?["status"]?.Value<string>();
        }
        catch
        {
            return null;
        }
    }

    public async Task EnsureWebhookSubscriptionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.CallbackBaseUrl) || Interlocked.CompareExchange(ref _webhookEnsured, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var url = _settings.CallbackBaseUrl.TrimEnd('/') + WebhookCallbackPath;
            var body = new Models.WebhookCreateRequest("eShopOnWeb Invest your change", url)
            {
                Type = new List<Models.Type> { Models.Type.All },
            };
            await _client.WebhookSubscriptionsApi.CreateWebhookAsync(_clientId, body: body, cancellationToken: cancellationToken);
            _logger.LogInformation("Registered Upvest webhook subscription for status changes.");
        }
        catch (Exception ex)
        {
            // Non-fatal: status is also reconciled by polling, so a missing webhook does not block anything.
            Interlocked.Exchange(ref _webhookEnsured, 0);
            _logger.LogWarning("Could not register Upvest webhook subscription: {Error}", ex.Message);
        }
    }

    private async Task<Guid> GetOrCreateAccountGroupAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Reuse an existing PERSONAL group if the user already has one (idempotent across restarts).
        var listed = await CallRawAsync("list-account-groups", () =>
            _client.AccountGroupsApi.ListUserAccountGroupsAsync(userId, _clientId, cancellationToken: cancellationToken));
        var existing = PickId(listed, item => (string?)item["type"] == "PERSONAL");
        if (existing is not null)
        {
            return existing.Value;
        }

        var body = Containers.CreateAccountGroupBody.FromAccountGroupCreateUserRequest(
            new Models.AccountGroupCreateUserRequest(userId, Models.Type13.Personal));
        var createdBody = await CallRawAsync("create-account-group", () => _client.AccountGroupsApi.CreateAccountGroupAsync(
            _clientId, Guid.NewGuid(), body: body, cancellationToken: cancellationToken));
        var created = ReadId(createdBody);
        if (created is not null)
        {
            return created.Value;
        }

        // Fall back to reading it from the list endpoint.
        var relisted = await CallRawAsync("list-account-groups", () =>
            _client.AccountGroupsApi.ListUserAccountGroupsAsync(userId, _clientId, cancellationToken: cancellationToken));
        return PickId(relisted, item => (string?)item["type"] == "PERSONAL")
            ?? throw new InvalidOperationException("Could not determine the Upvest account group id.");
    }

    private async Task<Guid> GetOrCreateTradingAccountAsync(Guid userId, Guid accountGroupId, CancellationToken cancellationToken)
    {
        var group = accountGroupId.ToString();
        bool IsTradingInGroup(JToken item) =>
            (string?)item["type"] == "TRADING" && (string?)item["account_group_id"] == group;

        var listed = await CallRawAsync("list-accounts", () =>
            _client.AccountsApi.ListUserAccountsAsync(userId, _clientId, cancellationToken: cancellationToken));
        var existing = PickId(listed, IsTradingInGroup) ?? PickId(listed, item => (string?)item["type"] == "TRADING");
        if (existing is not null)
        {
            return existing.Value;
        }

        var body = Containers.CreateAccountBody.FromAccountCreateUserRequest(
            new Models.AccountCreateUserRequest(userId, accountGroupId, Models.Type16.Trading) { Name = "Invest your change" });
        var createdBody = await CallRawAsync("create-account", () => _client.AccountsApi.CreateAccountAsync(
            _clientId, Guid.NewGuid(), body: body, cancellationToken: cancellationToken));
        var created = ReadId(createdBody);
        if (created is not null)
        {
            return created.Value;
        }

        var relisted = await CallRawAsync("list-accounts", () =>
            _client.AccountsApi.ListUserAccountsAsync(userId, _clientId, cancellationToken: cancellationToken));
        return PickId(relisted, IsTradingInGroup) ?? PickId(relisted, item => (string?)item["type"] == "TRADING")
            ?? throw new InvalidOperationException("Could not determine the Upvest trading account id.");
    }

    /// <summary>
    /// Runs a call and returns the raw response body the SDK received, tolerating the
    /// response-model deserialisation failures described on <see cref="UpvestRawResponseCallback"/>.
    /// A genuine HTTP error (4xx/5xx) is logged; its body is still returned so the caller can inspect it.
    /// </summary>
    private async Task<string?> CallRawAsync(string name, Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (ApiException ex)
        {
            var title = ex is ErrorException error ? error.Title : "error";
            _logger.LogWarning("Upvest '{Step}' returned HTTP {Code} {Title}", name, ((ApiException)ex).ResponseCode, title);
        }
        catch (Exception)
        {
            // Response body is valid but did not match the SDK's generated model; read it raw below.
        }

        return _rawResponse.LastBody;
    }

    private static Guid? ReadId(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            var id = (JToken.Parse(body) as JObject)?["id"]?.Value<string>();
            return id is null ? null : Guid.Parse(id);
        }
        catch
        {
            return null;
        }
    }

    private static Guid? PickId(string? listBody, Func<JToken, bool> match)
    {
        if (string.IsNullOrWhiteSpace(listBody))
        {
            return null;
        }

        try
        {
            if (JToken.Parse(listBody) is not JObject root || root["data"] is not JArray data)
            {
                return null;
            }

            var item = data.FirstOrDefault(match) ?? data.FirstOrDefault();
            var id = item?["id"]?.Value<string>();
            return id is null ? null : Guid.Parse(id);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Runs one onboarding call, returning its payload. On an Upvest error it logs the step and the
    /// server's error detail (no personal data) before rethrowing, so a failed onboarding is diagnosable.
    /// </summary>
    private async Task<T> Step<T>(string name, Func<Task<ApiResponse<T>>> call)
    {
        try
        {
            var response = await call();
            return response.Data;
        }
        catch (ApiException ex)
        {
            // Log only the status and the generic error title — never the detail or body, which could
            // echo the shopper's personal data.
            var title = ex is ErrorException error ? error.Title : "error";
            _logger.LogWarning("Upvest onboarding step '{Step}' failed: HTTP {Code} {Title}",
                name, ((ApiException)ex).ResponseCode, title);
            throw;
        }
    }

    private static Models.Country ParseCountry(string alpha2) =>
        JsonConvert.DeserializeObject<Models.Country>(Quote(alpha2));

    private static Models.Nationality ParseNationality(string alpha2) =>
        JsonConvert.DeserializeObject<Models.Nationality>(Quote(alpha2));

    private static Models.BirthCountry ParseBirthCountry(string alpha2) =>
        JsonConvert.DeserializeObject<Models.BirthCountry>(Quote(alpha2));

    private static string Quote(string value) => "\"" + (value ?? string.Empty).Trim().ToUpperInvariant() + "\"";
}
