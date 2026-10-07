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
using Newtonsoft.Json.Linq;
using UpvestInvestmentApi.Standard;
using Containers = UpvestInvestmentApi.Standard.Models.Containers;
using Models = UpvestInvestmentApi.Standard.Models;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Upvest;

/// <summary>
/// The application's single adapter onto the Upvest SDK. Every method here maps the shop's domain
/// concepts onto one or more SDK calls and translates the SDK's models back into the small result
/// types the application understands. Authentication is handled entirely by the shared signing handler
/// configured on the client, so nothing here touches credentials.
/// </summary>
public class UpvestInvestingGateway : IInvestingGateway
{
    private const string InstrumentIdTypeIsin = "ISIN";

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestResponseCapture _capture;
    private readonly UpvestOptions _options;
    private readonly IAppLogger<UpvestInvestingGateway> _logger;
    private readonly Guid _clientId;

    public UpvestInvestingGateway(
        UpvestInvestmentApiClient client,
        UpvestResponseCapture capture,
        IOptions<UpvestOptions> options,
        IAppLogger<UpvestInvestingGateway> logger)
    {
        _client = client;
        _capture = capture;
        _options = options.Value;
        _logger = logger;
        _clientId = Guid.Parse(_options.ClientId);
    }

    public async Task<CreateInvestorResult> CreateInvestorAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var address = new Models.Address(
            addressLine1: details.Address.Line1,
            postcode: details.Address.Postcode,
            country: ParseCountry(details.Address.Country),
            city: details.Address.City);

        var request = new Models.UserTolCreateRequest(
            firstName: details.FirstName,
            lastName: details.LastName,
            email: details.Email,
            birthDate: details.BirthDate.ToDateTime(TimeOnly.MinValue),
            nationalities: new List<Models.Nationality> { ParseNationality(details.Nationality) },
            address: address,
            fatca: new Models.Fatca(status: false, confirmedAt: DateTime.UtcNow))
        {
            PhoneNumber = details.PhoneNumber
        };

        var created = await _client.UsersApi.CreateUserAsync(
            _clientId, Guid.NewGuid(),
            body: Containers.CreateUserBody.FromUserTOLCreateRequest(request),
            cancellationToken: cancellationToken);

        var userId = created.Data.Match(byol => byol.Id, tol => tol.Id);

        // Record tax residency (tax id + country) separately, as the API models it apart from the user.
        try
        {
            var taxResidency = Containers.TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
                new Models.WithTaxIdentifierNumber(ParseCountry(details.TaxCountry), details.TaxId));
            await _client.TaxResidenciesApi.SetTaxResidenciesAsync(
                userId, _clientId, Guid.NewGuid(),
                body: new Models.TaxResidenciesSetRequest(new List<Containers.TaxResidenciesSetRequestTaxResidencies> { taxResidency }),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Setting tax residency for upvest user {0} failed: {1}", userId, ex.Message);
        }

        // Report the shopper's identity verification to Upvest so it can accept them as an investor.
        try
        {
            var kyc = new Models.UserCheckKnowYourCustomerCreateRequest(
                type: "KYC",
                checkConfirmedAt: DateTime.UtcNow,
                dataDownloadLink: "https://kyc.eshoponweb.example/evidence/" + userId,
                documentType: Models.DocumentType3.IdCard,
                provider: "eShopOnWeb",
                method: Models.Method.ElectronicId)
            {
                Nationality = details.Nationality
            };
            await _client.UserChecksApi.CreateUserCheckAsync(
                userId, _clientId,
                body: Containers.CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(kyc),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Submitting KYC for upvest user {0} failed: {1}", userId, ex.Message);
        }

        EnrolmentStatus status;
        try
        {
            status = await DetermineEnrolmentStatusAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Determining initial enrolment status for upvest user {0} failed: {1}", userId, ex.Message);
            status = EnrolmentStatus.Pending;
        }

        return new CreateInvestorResult(userId, status);
    }

    public Task<EnrolmentStatus> GetEnrolmentStatusAsync(Guid upvestUserId, CancellationToken cancellationToken = default)
        => DetermineEnrolmentStatusAsync(upvestUserId, cancellationToken);

    public async Task<UpvestAccountRef> EnsureAccountAsync(Guid upvestUserId, CancellationToken cancellationToken = default)
    {
        // Create the shopper's cash account group, then a trading account within it. The ids are read
        // from the raw responses because this SDK version's account models do not match the provider's
        // current payloads (see UpvestResponseCapture).
        var groupBody = await CallAndReadRawAsync(
            ct => _client.AccountGroupsApi.CreateAccountGroupAsync(
                _clientId, Guid.NewGuid(),
                body: Containers.CreateAccountGroupBody.FromAccountGroupCreateUserRequest(
                    new Models.AccountGroupCreateUserRequest(upvestUserId, Models.Type13.Personal)),
                cancellationToken: ct),
            cancellationToken);
        var accountGroupId = Guid.Parse((string)groupBody["id"]!);

        var accountBody = await CallAndReadRawAsync(
            ct => _client.AccountsApi.CreateAccountAsync(
                _clientId, Guid.NewGuid(),
                body: Containers.CreateAccountBody.FromAccountCreateUserRequest(
                    new Models.AccountCreateUserRequest(upvestUserId, accountGroupId, Models.Type16.Trading)),
                cancellationToken: ct),
            cancellationToken);
        var accountId = Guid.Parse((string)accountBody["id"]!);
        var resolvedGroupId = accountBody["account_group_id"] is { } g ? Guid.Parse((string)g!) : accountGroupId;

        return new UpvestAccountRef(accountId, resolvedGroupId);
    }

    public async Task AddCashAsync(Guid accountGroupId, decimal amountEur, CancellationToken cancellationToken = default)
    {
        // Raw-read: tolerate the provider/SDK model skew on the response (the cash increase still happens).
        await CallAndReadRawAsync(
            ct => _client.VirtualCashBalancesApi.CreateVirtualCashIncreaseAsync(
                _clientId, Guid.NewGuid(),
                body: new Models.VirtualCashBalanceVirtualCashIncreaseCreateRequest(
                    accountGroupId, FormatAmount(amountEur), Models.Currency1.Eur),
                cancellationToken: ct),
            cancellationToken);
    }

    public async Task<PlaceInvestmentResult> PlaceInvestmentOrderAsync(Guid upvestUserId, Guid accountId, decimal amountEur, CancellationToken cancellationToken = default)
    {
        var request = new Models.OrderPlaceRequest(
            accountId: accountId,
            side: Models.Side.Buy,
            instrumentId: _options.InstrumentId,
            instrumentIdType: InstrumentIdTypeIsin)
        {
            UserId = upvestUserId,
            CashAmount = FormatAmount(amountEur),
            Currency = Models.Currency29.Eur
        };

        var body = await CallAndReadRawAsync(
            ct => _client.OrdersApi.PlaceOrderAsync(_clientId, Guid.NewGuid(), body: request, cancellationToken: ct),
            cancellationToken);

        return new PlaceInvestmentResult(Guid.Parse((string)body["id"]!), MapOrderStatus((string?)body["status"]));
    }

    public async Task<InvestmentStatus> GetInvestmentStatusAsync(Guid upvestOrderId, CancellationToken cancellationToken = default)
    {
        var body = await CallAndReadRawAsync(
            ct => _client.OrdersApi.RetrieveOrderAsync(upvestOrderId, _clientId, cancellationToken: ct),
            cancellationToken);
        return MapOrderStatus((string?)body["status"]);
    }

    public async Task EnsureSettlementWebhookAsync(string callbackUrl, CancellationToken cancellationToken = default)
    {
        var existing = await _client.WebhookSubscriptionsApi.ListWebhooksAsync(_clientId, cancellationToken: cancellationToken);
        if (existing.Data?.Data?.Any(w => string.Equals(w.Url, callbackUrl, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return;
        }

        await _client.WebhookSubscriptionsApi.CreateWebhookAsync(
            _clientId,
            body: new Models.WebhookCreateRequest(
                title: "eShopOnWeb invest-your-change",
                url: callbackUrl,
                type: new List<Models.Type>
                {
                    Models.Type.User, Models.Type.UserCheck, Models.Type.Order, Models.Type.Execution
                }),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Registered Upvest webhook subscription for settlement events.");
    }

    // --- helpers -----------------------------------------------------------------------------

    /// <summary>
    /// Runs an SDK call and returns the raw JSON body of its response. A genuine provider error
    /// (non-2xx) is rethrown; a typed-deserialization failure on a successful response is tolerated,
    /// since the body was still captured and can be read directly.
    /// </summary>
    private async Task<JObject> CallAndReadRawAsync(Func<CancellationToken, Task> sdkCall, CancellationToken cancellationToken)
    {
        _capture.Begin();
        try
        {
            await sdkCall(cancellationToken);
        }
        catch (UpvestInvestmentApi.Standard.Exceptions.ApiException)
        {
            throw; // a real HTTP error from the provider
        }
        catch (Exception)
        {
            // Typed model did not match a well-formed 2xx body; fall through and read it raw.
        }

        var body = _capture.LastBody;
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException("No Upvest response body was captured.");
        }
        return JObject.Parse(body);
    }

    // --- mapping helpers ---------------------------------------------------------------------

    private enum KycOutcome { Unknown, InProgress, Passed, Failed }

    private async Task<EnrolmentStatus> DetermineEnrolmentStatusAsync(Guid upvestUserId, CancellationToken cancellationToken)
    {
        var kyc = await GetKycOutcomeAsync(upvestUserId, cancellationToken);
        if (kyc == KycOutcome.Failed)
        {
            return EnrolmentStatus.Rejected;
        }

        var userResponse = await _client.UsersApi.RetrieveUserAsync(upvestUserId, _clientId, cancellationToken: cancellationToken);
        var userStatus = userResponse.Data.Match(byol => byol.Status, tol => tol.Status);

        if (userStatus is Models.Status.Offboarding or Models.Status.Offboarded)
        {
            return EnrolmentStatus.Rejected;
        }
        // Upvest accepts the shopper by activating the user once KYC has passed.
        if (userStatus == Models.Status.Active && kyc != KycOutcome.InProgress)
        {
            return EnrolmentStatus.Active;
        }
        return EnrolmentStatus.Pending;
    }

    /// <summary>
    /// Read the KYC check outcome from the raw response. The SDK's check models do not deserialize the
    /// provider's current payloads (the oneOf matches several variants), so the body is read directly.
    /// </summary>
    private async Task<KycOutcome> GetKycOutcomeAsync(Guid upvestUserId, CancellationToken cancellationToken)
    {
        try
        {
            var body = await CallAndReadRawAsync(
                ct => _client.UserChecksApi.ListUserChecksAsync(upvestUserId, _clientId, type: Models.Type6.Kyc, cancellationToken: ct),
                cancellationToken);

            if (body["data"] is not JArray data)
            {
                return KycOutcome.Unknown;
            }

            var outcome = KycOutcome.Unknown;
            foreach (var item in data)
            {
                if (!string.Equals((string?)item["type"], "KYC", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                switch (((string?)item["status"])?.Trim().ToUpperInvariant())
                {
                    case "FAILED":
                    case "REJECTED":
                        return KycOutcome.Failed;
                    case "PASSED":
                        outcome = KycOutcome.Passed;
                        break;
                    default: // CREATED / IN_PROGRESS / PENDING
                        if (outcome == KycOutcome.Unknown)
                        {
                            outcome = KycOutcome.InProgress;
                        }
                        break;
                }
            }
            return outcome;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Reading KYC checks for upvest user {0} failed: {1}", upvestUserId, ex.Message);
            return KycOutcome.Unknown;
        }
    }

    /// <summary>
    /// Map Upvest's order status (read raw, as the SDK's order model does not deserialize the provider's
    /// payload) onto this app's investment status. Terminal success → settled, terminal non-success →
    /// failed, anything in flight → pending.
    /// </summary>
    private static InvestmentStatus MapOrderStatus(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "FILLED" or "SETTLED" or "COMPLETED" or "EXECUTED" or "MATCHED" => InvestmentStatus.Settled,
        "CANCELLED" or "CANCELED" or "FAILED" or "REJECTED" or "EXPIRED" => InvestmentStatus.Failed,
        _ => InvestmentStatus.Pending
    };

    private static string FormatAmount(decimal amountEur) => amountEur.ToString("0.00", CultureInfo.InvariantCulture);

    private static Models.Nationality ParseNationality(string alpha2)
    {
        if (!string.IsNullOrWhiteSpace(alpha2) && Enum.TryParse<Models.Nationality>(alpha2, ignoreCase: true, out var value))
        {
            return value;
        }
        throw new ArgumentException($"Unsupported nationality code '{alpha2}'.", nameof(alpha2));
    }

    private static Models.Country ParseCountry(string alpha2)
    {
        if (!string.IsNullOrWhiteSpace(alpha2) && Enum.TryParse<Models.Country>(alpha2, ignoreCase: true, out var value))
        {
            return value;
        }
        throw new ArgumentException($"Unsupported country code '{alpha2}'.", nameof(alpha2));
    }
}
