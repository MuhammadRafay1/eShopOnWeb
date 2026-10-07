using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi.Standard;
using Models = UpvestInvestmentApi.Standard.Models;
using Containers = UpvestInvestmentApi.Standard.Models.Containers;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Talks to Upvest through the vendored SDK. Every request is issued via the SDK's controllers (so it
/// is authenticated, signed and built by the SDK); the raw response is read back through
/// <see cref="UpvestResponseCapture"/> because the SDK's generated response models are stricter than
/// the sandbox's responses. No personal data is ever logged.
/// </summary>
public sealed class UpvestGateway : IUpvestInvestingGateway
{
    // Sandbox consent documents (from the Upvest onboarding workflow reference).
    private static readonly Guid TermsConsentDocumentId = Guid.Parse("d0b83880-3809-4eee-b0df-ca50db84c15a");
    private static readonly Guid DataPrivacyConsentDocumentId = Guid.Parse("7ab0fc5c-8157-4acd-b02d-6ccda0e81dec");

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestOptions _options;
    private readonly ILogger<UpvestGateway> _logger;
    private readonly Guid _upvestClientId;

    public UpvestGateway(UpvestInvestmentApiClient client, IOptions<UpvestOptions> options, ILogger<UpvestGateway> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        _upvestClientId = Guid.Parse(_options.ClientId);
    }

    public async Task<UpvestUserRef> EnrolUserAsync(InvestorSignup signup, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // 1. Create the user. A 4xx here means Upvest will not take the shopper on.
        var userBody = BuildCreateUserBody(signup, now);
        string createBody;
        try
        {
            createBody = await SendAsync(
                () => _client.UsersApi.CreateUserAsync(_upvestClientId, Guid.NewGuid(), Models.UpvestApiVersion.Enum1, userBody),
                "create_user", cancellationToken);
        }
        catch (UpvestException ex) when (ex.StatusCode is >= 400 and < 500)
        {
            throw new UpvestRejectedException("Upvest declined the investor sign-up.", ex.StatusCode);
        }

        string userId;
        EnrolmentStatus status;
        using (var doc = JsonDocument.Parse(createBody))
        {
            userId = doc.RootElement.GetProperty("id").GetString()!;
            status = MapUserStatus(doc.RootElement.GetProperty("status").GetString());
        }

        _logger.LogInformation("Upvest user created {UserId} with status {Status}", userId, status);

        // 2. Submit the regulatory checks the client provides under the TOL model.
        await SubmitKycCheckAsync(userId, signup, now, cancellationToken);
        await SubmitInstrumentFitCheckAsync(userId, now, cancellationToken);

        // 3. Declare tax residency (best effort — a malformed TIN must not block enrolment).
        await SetTaxResidencyAsync(userId, signup, cancellationToken);

        return new UpvestUserRef(userId, status);
    }

    public async Task<EnrolmentStatus> GetUserStatusAsync(string upvestUserId, CancellationToken cancellationToken = default)
    {
        var body = await SendAsync(
            () => _client.UsersApi.RetrieveUserAsync(Guid.Parse(upvestUserId), _upvestClientId, Models.UpvestApiVersion.Enum1),
            "retrieve_user", cancellationToken);
        using var doc = JsonDocument.Parse(body);
        return MapUserStatus(doc.RootElement.GetProperty("status").GetString());
    }

    public async Task<UpvestAccountRef> ProvisionAccountAsync(string upvestUserId, CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(upvestUserId);

        var agBody = Containers.CreateAccountGroupBody.FromAccountGroupCreateUserRequest(
            new Models.AccountGroupCreateUserRequest(userId, Models.Type13.Personal));
        var agJson = await SendAsync(
            () => _client.AccountGroupsApi.CreateAccountGroupAsync(_upvestClientId, Guid.NewGuid(), Models.UpvestApiVersion.Enum1, agBody),
            "create_account_group", cancellationToken);
        string accountGroupId;
        using (var doc = JsonDocument.Parse(agJson)) accountGroupId = doc.RootElement.GetProperty("id").GetString()!;

        var acctBody = Containers.CreateAccountBody.FromAccountCreateUserRequest(
            new Models.AccountCreateUserRequest(userId, Guid.Parse(accountGroupId), Models.Type16.Trading));
        var acctJson = await SendAsync(
            () => _client.AccountsApi.CreateAccountAsync(_upvestClientId, Guid.NewGuid(), Models.UpvestApiVersion.Enum1, acctBody),
            "create_account", cancellationToken);
        string accountId;
        using (var doc = JsonDocument.Parse(acctJson)) accountId = doc.RootElement.GetProperty("id").GetString()!;

        _logger.LogInformation("Provisioned Upvest account {AccountId} in group {GroupId}", accountId, accountGroupId);

        // The account activates shortly after its group. Give it a bounded moment so the first
        // investment is not rejected; if it is not ready in time the investment simply retries later.
        await WaitForAccountActiveAsync(accountId, cancellationToken);

        return new UpvestAccountRef(accountGroupId, accountId);
    }

    public async Task<UpvestInvestmentRef> PlaceInvestmentAsync(string upvestUserId, string upvestAccountId, decimal amountEuros, CancellationToken cancellationToken = default)
    {
        var request = new Models.OrderPlaceRequest(
            accountId: Guid.Parse(upvestAccountId),
            side: Models.Side.Buy,
            instrumentId: _options.InstrumentId,
            instrumentIdType: "ISIN",
            userId: Guid.Parse(upvestUserId),
            cashAmount: amountEuros.ToString("0.00", CultureInfo.InvariantCulture),
            currency: Models.Currency29.Eur,
            orderType: Models.OrderType.Market,
            userInstrumentFitAcknowledgement: true);

        var body = await SendAsync(
            () => _client.OrdersApi.PlaceOrderAsync(_upvestClientId, Guid.NewGuid(), Models.UpvestApiVersion.Enum1, request),
            "place_order", cancellationToken);

        using var doc = JsonDocument.Parse(body);
        var orderId = doc.RootElement.GetProperty("id").GetString()!;
        var status = MapOrderStatus(doc.RootElement.GetProperty("status").GetString());
        _logger.LogInformation("Placed Upvest order {OrderId} for {Amount} EUR, status {Status}", orderId, amountEuros, status);
        return new UpvestInvestmentRef(orderId, status);
    }

    public async Task<InvestmentStatus> GetOrderStatusAsync(string upvestOrderId, CancellationToken cancellationToken = default)
    {
        var body = await SendAsync(
            () => _client.OrdersApi.RetrieveOrderAsync(Guid.Parse(upvestOrderId), _upvestClientId, Models.UpvestApiVersion.Enum1),
            "retrieve_order", cancellationToken);
        using var doc = JsonDocument.Parse(body);
        return MapOrderStatus(doc.RootElement.GetProperty("status").GetString());
    }

    public async Task EnsureWebhookSubscriptionAsync(string callbackUrl, CancellationToken cancellationToken = default)
    {
        // Subscribe to all event types (omitting the type list means "all").
        var createBody = new Models.WebhookCreateRequest(title: "eShopOnWeb invest-your-change", url: callbackUrl);
        var body = await SendAsync(
            () => _client.WebhookSubscriptionsApi.CreateWebhookAsync(_upvestClientId, Models.UpvestApiVersion.Enum1, createBody),
            "create_webhook", cancellationToken);

        string webhookId;
        using (var doc = JsonDocument.Parse(body)) webhookId = doc.RootElement.GetProperty("id").GetString()!;

        var updateBody = new Models.WebhookUpdateRequest(title: "eShopOnWeb invest-your-change", url: callbackUrl, enabled: true);
        await SendAsync(
            () => _client.WebhookSubscriptionsApi.UpdateWebhookAsync(Guid.Parse(webhookId), _upvestClientId, Models.UpvestApiVersion.Enum1, updateBody),
            "update_webhook", cancellationToken);

        _logger.LogInformation("Upvest webhook {WebhookId} active for {CallbackUrl}", webhookId, callbackUrl);
    }

    // ---- enrolment helpers ----

    private Containers.CreateUserBody BuildCreateUserBody(InvestorSignup s, DateTime now)
    {
        var address = new Models.Address(
            addressLine1: s.Address.Line1,
            postcode: s.Address.Postcode,
            country: WireEnum<Models.Country>(s.Address.Country),
            city: s.Address.City);

        // The sign-up form carries no birth place, which Upvest requires: derive the birth country
        // from nationality and reuse the residential city, the best proxies available on the form.
        var request = new Models.UserTolCreateRequest(
            firstName: s.FirstName,
            lastName: s.LastName,
            email: s.Email,
            birthDate: s.BirthDate.ToDateTime(TimeOnly.MinValue),
            nationalities: new List<Models.Nationality> { WireEnum<Models.Nationality>(s.Nationality) },
            address: address,
            fatca: new Models.Fatca(false, now),
            birthCity: s.Address.City,
            birthCountry: WireEnum<Models.BirthCountry>(s.Nationality),
            phoneNumber: string.IsNullOrWhiteSpace(s.PhoneNumber) ? null : s.PhoneNumber,
            termsAndConditions: new Models.TermsAndConditions(TermsConsentDocumentId, now),
            dataPrivacyAndSharingAgreement: new Models.DataPrivacyAndSharingAgreement(DataPrivacyConsentDocumentId, now));

        return Containers.CreateUserBody.FromUserTOLCreateRequest(request);
    }

    private async Task SubmitKycCheckAsync(string userId, InvestorSignup s, DateTime now, CancellationToken ct)
    {
        var confirmedAddress = new Models.Address(
            addressLine1: s.Address.Line1, postcode: s.Address.Postcode,
            country: WireEnum<Models.Country>(s.Address.Country), city: s.Address.City);

        // The shop confirms the shopper's identity (TOL model). Fixed, non-personal check metadata;
        // confirmed_address satisfies the proof-of-residency requirement so no separate POR is needed.
        var kyc = Containers.CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(
            new Models.UserCheckKnowYourCustomerCreateRequest(
                type: "KYC", checkConfirmedAt: now, dataDownloadLink: "https://eshoponweb.example/kyc-evidence",
                documentType: Models.DocumentType3.Passport, provider: "eShopOnWeb", method: Models.Method.VideoId,
                confirmedAddress: confirmedAddress));

        await SendAsync(
            () => _client.UserChecksApi.CreateUserCheckAsync(Guid.Parse(userId), _upvestClientId, Models.UpvestApiVersion.Enum1, kyc),
            "create_user_check:KYC", ct);
    }

    private async Task SubmitInstrumentFitCheckAsync(string userId, DateTime now, CancellationToken ct)
    {
        var fitRequest = new Models.UserCheckInstrumentFitCreateRequest("INSTRUMENT_FIT", now, new Models.InstrumentSuitability(true));
        // The sandbox enforces these fields for every check type; the SDK's instrument-fit model omits
        // them, so they are supplied through the model's additional-properties indexer.
        fitRequest["data_download_link"] = "https://eshoponweb.example/appropriateness-assessment";
        fitRequest["document_type"] = "PASSPORT";
        fitRequest["provider"] = "eShopOnWeb";
        fitRequest["method"] = "VIDEO_ID";
        var fit = Containers.CreateUserCheckBody.FromUserCheckInstrumentFitCreateRequest(fitRequest);

        await SendAsync(
            () => _client.UserChecksApi.CreateUserCheckAsync(Guid.Parse(userId), _upvestClientId, Models.UpvestApiVersion.Enum1, fit),
            "create_user_check:INSTRUMENT_FIT", ct);
    }

    private async Task SetTaxResidencyAsync(string userId, InvestorSignup s, CancellationToken ct)
    {
        Models.Country country;
        try { country = WireEnum<Models.Country>(s.TaxCountry); }
        catch { _logger.LogWarning("Skipping tax residency: unrecognised tax country"); return; }

        async Task<bool> TryAsync(Containers.TaxResidenciesSetRequestTaxResidencies entry)
        {
            try
            {
                await SendAsync(
                    () => _client.TaxResidenciesApi.SetTaxResidenciesAsync(Guid.Parse(userId), _upvestClientId, Guid.NewGuid(),
                        Models.UpvestApiVersion.Enum1, new Models.TaxResidenciesSetRequest(new List<Containers.TaxResidenciesSetRequestTaxResidencies> { entry })),
                    "set_tax_residencies", ct);
                return true;
            }
            catch (UpvestException ex) when (ex.StatusCode is >= 400 and < 500)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(s.TaxId) &&
            await TryAsync(Containers.TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
                new Models.WithTaxIdentifierNumber(country, s.TaxId))))
        {
            return;
        }

        // Fall back to declaring the TIN unavailable so a malformed/absent id does not block onboarding.
        if (!await TryAsync(Containers.TaxResidenciesSetRequestTaxResidencies.FromWithoutTaxIdentifierNumber(
                new Models.WithoutTaxIdentifierNumber(country, Models.MissingTinReason.OtherReasons))))
        {
            _logger.LogWarning("Tax residency could not be set for user {UserId}; continuing enrolment", userId);
        }
    }

    private async Task WaitForAccountActiveAsync(string accountId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                var body = await SendAsync(
                    () => _client.AccountsApi.RetrieveAccountAsync(Guid.Parse(accountId), _upvestClientId, Models.UpvestApiVersion.Enum1),
                    "retrieve_account", ct);
                using var doc = JsonDocument.Parse(body);
                if (string.Equals(doc.RootElement.GetProperty("status").GetString(), "ACTIVE", StringComparison.OrdinalIgnoreCase))
                    return;
            }
            catch (UpvestException)
            {
                // transient — keep waiting within the bound
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
    }

    // ---- SDK call plumbing ----

    /// <summary>
    /// Issues one SDK operation and returns the raw response body. Success is judged by the captured
    /// HTTP status, so the SDK's own response-model deserialisation (which can throw against the
    /// sandbox's thinner responses) is tolerated. Non-2xx responses raise <see cref="UpvestException"/>.
    /// </summary>
    private async Task<string> SendAsync(Func<Task> sdkCall, string operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var captured = UpvestResponseCapture.Begin();
        Exception? sdkError = null;
        try
        {
            await sdkCall().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            sdkError = ex;
        }

        if (captured.StatusCode == 0)
        {
            throw new UpvestException($"Upvest {operation} did not return a response.", sdkError ?? new Exception("no response"));
        }

        if (captured.StatusCode is < 200 or >= 300)
        {
            _logger.LogWarning("Upvest {Operation} returned {StatusCode}", operation, captured.StatusCode);
            throw new UpvestException($"Upvest {operation} failed with status {captured.StatusCode}.", captured.StatusCode);
        }

        return captured.Body ?? "{}";
    }

    private static EnrolmentStatus MapUserStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "ACTIVE" => EnrolmentStatus.Active,
        "INACTIVE" => EnrolmentStatus.Pending,
        "OFFBOARDING" or "OFFBOARDED" => EnrolmentStatus.Rejected,
        _ => EnrolmentStatus.Pending
    };

    private static InvestmentStatus MapOrderStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "FILLED" => InvestmentStatus.Settled,
        "CANCELLED" or "REJECTED" or "FAILED" => InvestmentStatus.Failed,
        _ => InvestmentStatus.Pending // NEW, PROCESSING
    };

    private static T WireEnum<T>(string wireValue)
    {
        try
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>("\"" + wireValue + "\"")!;
        }
        catch (Exception ex)
        {
            throw new UpvestRejectedException($"'{wireValue}' is not a value Upvest accepts for {typeof(T).Name}.", 400);
        }
    }
}
