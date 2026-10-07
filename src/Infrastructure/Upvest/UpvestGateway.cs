using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Containers = UpvestInvestmentApi.Standard.Models.Containers;
using Models = UpvestInvestmentApi.Standard.Models;
using UpvestClient = UpvestInvestmentApi.Standard.UpvestInvestmentApiClient;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Implements the application's Upvest boundary on top of the uv plugin's SDK.
/// Every method drives a generated controller; the single <see cref="UpvestSigningHandler"/>
/// beneath the SDK's HttpClient authenticates (signs) each request.
///
/// The live mock returns some resource bodies in a shape the SDK's generated
/// models reject (e.g. <c>user_id</c> where the model requires <c>users</c>), so
/// for those calls we read the id/status from the captured raw body instead of
/// the thrown typed result. The HTTP call itself still succeeds.
/// </summary>
public sealed class UpvestGateway : IUpvestGateway
{
    // Upvest sandbox consent document ids (from the onboarding workflow package).
    private static readonly Guid TermsConsentId = Guid.Parse("d0b83880-3809-4eee-b0df-ca50db84c15a");
    private static readonly Guid DataPrivacyConsentId = Guid.Parse("7ab0fc5c-8157-4acd-b02d-6ccda0e81dec");

    private readonly UpvestClient _client;
    private readonly UpvestOptions _options;
    private readonly Guid _upvestClientId;
    private readonly ILogger<UpvestGateway> _logger;

    public UpvestGateway(UpvestClient client, IOptions<UpvestOptions> options, ILogger<UpvestGateway> logger)
    {
        _client = client;
        _options = options.Value;
        _upvestClientId = Guid.Parse(_options.ClientId);
        _logger = logger;
    }

    public async Task<Guid> CreateInvestorAsync(InvestorDetails details, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var address = new Models.Address
        {
            AddressLine1 = details.Address.Line1,
            Postcode = details.Address.Postcode,
            City = details.Address.City,
            Country = ParseEnum<Models.Country>(details.Address.Country)
        };

        var userBody = Containers.CreateUserBody.FromUserTOLCreateRequest(new Models.UserTolCreateRequest
        {
            FirstName = details.FirstName,
            LastName = details.LastName,
            Email = details.Email,
            BirthDate = details.BirthDate.ToDateTime(TimeOnly.MinValue),
            BirthCity = details.Address.City,
            BirthCountry = ParseEnum<Models.BirthCountry>(details.Nationality),
            Nationalities = new List<Models.Nationality> { ParseEnum<Models.Nationality>(details.Nationality) },
            Address = address,
            PhoneNumber = details.PhoneNumber,
            Fatca = new Models.Fatca { Status = false, ConfirmedAt = now },
            TermsAndConditions = new Models.TermsAndConditions { ConsentDocumentId = TermsConsentId, ConfirmedAt = now },
            DataPrivacyAndSharingAgreement = new Models.DataPrivacyAndSharingAgreement { ConsentDocumentId = DataPrivacyConsentId, ConfirmedAt = now }
        });

        var created = await SendAsync(() => _client.UsersApi.CreateUserAsync(_upvestClientId, Guid.NewGuid(), body: userBody), ct);
        var userId = Guid.Parse((string)created!["id"]!);

        // KYC check (confirmed_address satisfies the proof-of-residency requirement).
        var kyc = Containers.CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(new Models.UserCheckKnowYourCustomerCreateRequest
        {
            Type = "KYC",
            CheckConfirmedAt = now,
            DataDownloadLink = "https://eshoponweb.example/kyc-evidence.zip",
            DocumentType = Models.DocumentType3.Passport,
            Provider = "eShopOnWeb",
            Method = Models.Method.ElectronicId,
            ConfirmedAddress = address
        });
        await SendAsync(() => _client.UserChecksApi.CreateUserCheckAsync(userId, _upvestClientId, body: kyc), ct);

        // Appropriateness / INSTRUMENT_FIT check. The SDK model omits fields the
        // backend requires, so supply them through its additional-properties indexer.
        var fitReq = new Models.UserCheckInstrumentFitCreateRequest
        {
            Type = "INSTRUMENT_FIT",
            CheckConfirmedAt = now,
            InstrumentSuitability = new Models.InstrumentSuitability { Suitability = true }
        };
        fitReq["data_download_link"] = "https://eshoponweb.example/appropriateness.zip";
        fitReq["document_type"] = "PASSPORT";
        fitReq["provider"] = "eShopOnWeb";
        fitReq["method"] = "ELECTRONIC_ID";
        var fit = Containers.CreateUserCheckBody.FromUserCheckInstrumentFitCreateRequest(fitReq);
        await SendAsync(() => _client.UserChecksApi.CreateUserCheckAsync(userId, _upvestClientId, body: fit), ct);

        // Tax residency.
        var tax = new Models.TaxResidenciesSetRequest(new List<Containers.TaxResidenciesSetRequestTaxResidencies>
        {
            Containers.TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
                new Models.WithTaxIdentifierNumber
                {
                    Country = ParseEnum<Models.Country>(details.TaxCountry),
                    TaxIdentifierNumber = details.TaxId
                })
        });
        await SendAsync(() => _client.TaxResidenciesApi.SetTaxResidenciesAsync(userId, _upvestClientId, Guid.NewGuid(), body: tax), ct);

        return userId;
    }

    public async Task<string> GetUserStatusAsync(Guid upvestUserId, CancellationToken ct)
    {
        var body = await SendAsync(() => _client.UsersApi.RetrieveUserAsync(upvestUserId, _upvestClientId), ct);
        return (string?)body?["status"] ?? "UNKNOWN";
    }

    public async Task<Guid> CreateAccountGroupAsync(Guid upvestUserId, CancellationToken ct)
    {
        var body = Containers.CreateAccountGroupBody.FromAccountGroupCreateUserRequest(
            new Models.AccountGroupCreateUserRequest { UserId = upvestUserId, Type = Models.Type13.Personal });
        var created = await SendAsync(() => _client.AccountGroupsApi.CreateAccountGroupAsync(_upvestClientId, Guid.NewGuid(), body: body), ct);
        return Guid.Parse((string)created!["id"]!);
    }

    public async Task<(Guid AccountId, string Status)> CreateAccountAsync(Guid upvestUserId, Guid accountGroupId, CancellationToken ct)
    {
        var body = Containers.CreateAccountBody.FromAccountCreateUserRequest(
            new Models.AccountCreateUserRequest { UserId = upvestUserId, AccountGroupId = accountGroupId, Type = Models.Type16.Trading });
        var created = await SendAsync(() => _client.AccountsApi.CreateAccountAsync(_upvestClientId, Guid.NewGuid(), body: body), ct);
        return (Guid.Parse((string)created!["id"]!), (string?)created["status"] ?? "UNKNOWN");
    }

    public async Task<string> GetAccountStatusAsync(Guid accountId, CancellationToken ct)
    {
        var body = await SendAsync(() => _client.AccountsApi.RetrieveAccountAsync(accountId, _upvestClientId), ct);
        return (string?)body?["status"] ?? "UNKNOWN";
    }

    public async Task FundAsync(Guid accountGroupId, decimal amount, CancellationToken ct)
    {
        var body = new Models.VirtualCashBalanceVirtualCashIncreaseCreateRequest
        {
            AccountGroupId = accountGroupId,
            Amount = Money(amount),
            Currency = Models.Currency1.Eur
        };
        await SendAsync(() => _client.VirtualCashBalancesApi.CreateVirtualCashIncreaseAsync(_upvestClientId, Guid.NewGuid(), body: body), ct);
    }

    public async Task<Guid> PlaceInvestmentOrderAsync(Guid upvestUserId, Guid accountId, decimal amount, CancellationToken ct)
    {
        var body = new Models.OrderPlaceRequest
        {
            UserId = upvestUserId,
            AccountId = accountId,
            Side = Models.Side.Buy,
            InstrumentId = _options.InstrumentId,
            InstrumentIdType = "ISIN",
            CashAmount = Money(amount),
            Currency = Models.Currency29.Eur,
            OrderType = Models.OrderType.Market,
            UserInstrumentFitAcknowledgement = true
        };
        var created = await SendAsync(() => _client.OrdersApi.PlaceOrderAsync(_upvestClientId, Guid.NewGuid(), body: body), ct);
        return Guid.Parse((string)created!["id"]!);
    }

    public async Task<string> GetOrderStatusAsync(Guid orderId, CancellationToken ct)
    {
        var body = await SendAsync(() => _client.OrdersApi.RetrieveOrderAsync(orderId, _upvestClientId), ct);
        return (string?)body?["status"] ?? "UNKNOWN";
    }

    /// <summary>
    /// Runs an SDK call and returns the response body as a <see cref="JObject"/>.
    /// Treats any 2xx as success even if the SDK's typed deserialization threw
    /// (the mock's bodies do not always match the generated models). Re-throws on
    /// a genuine non-2xx response.
    /// </summary>
    private static async Task<JObject?> SendAsync(Func<Task> call, CancellationToken ct)
    {
        using var capture = UpvestRawResponse.Capture();
        Exception? thrown = null;
        try
        {
            await call();
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        if (capture.StatusCode is >= 200 and < 300)
            return string.IsNullOrWhiteSpace(capture.Body) ? null : JObject.Parse(capture.Body!);

        if (thrown != null) throw thrown;
        throw new InvalidOperationException($"Upvest call returned status {capture.StatusCode?.ToString() ?? "none"}.");
    }

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static T ParseEnum<T>(string isoCode) =>
        JsonConvert.DeserializeObject<T>("\"" + isoCode.Trim().ToUpperInvariant() + "\"", new StringEnumConverter())!;
}
