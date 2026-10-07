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
using Microsoft.Extensions.Options;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Utilities;
using Containers = UpvestInvestmentApi.Standard.Models.Containers;
using Models = UpvestInvestmentApi.Standard.Models;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Talks to Upvest through the vendored APIMatic SDK. This is the only type in the solution
/// that references the SDK. It translates the application's plain requests into SDK calls and
/// maps SDK responses back into the domain enums on <see cref="IUpvestInvestingGateway"/>.
///
/// Every call carries a fresh idempotency key and goes through the client whose HTTP pipeline
/// is the single signing handler (configured where the client is built).
/// </summary>
public class UpvestInvestingGateway : IUpvestInvestingGateway
{
    // Sandbox consent document ids documented in the Upvest onboarding workflow package.
    private static readonly Guid TermsAndConditionsDocumentId = Guid.Parse("d0b83880-3809-4eee-b0df-ca50db84c15a");
    private static readonly Guid DataPrivacyDocumentId = Guid.Parse("7ab0fc5c-8157-4acd-b02d-6ccda0e81dec");

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestRawHttpClient _raw;
    private readonly UpvestSettings _settings;
    private readonly Guid _upvestClientId;

    public UpvestInvestingGateway(UpvestInvestmentApiClient client, UpvestRawHttpClient raw, IOptions<UpvestSettings> settings)
    {
        _client = client;
        _raw = raw;
        _settings = settings.Value;
        _upvestClientId = Guid.Parse(_settings.ClientId);
    }

    public async Task<UpvestEnrolmentResult> EnrolInvestorAsync(UpvestEnrolmentRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var address = new Models.Address(
            addressLine1: request.AddressLine1,
            postcode: request.Postcode,
            country: ParseEnum<Models.Country>(request.Country),
            city: request.City);

        // 1. Create the user (starts INACTIVE until checks pass).
        var userBody = Containers.CreateUserBody.FromUserTOLCreateRequest(new Models.UserTolCreateRequest(
            firstName: request.FirstName,
            lastName: request.LastName,
            email: request.Email,
            birthDate: request.BirthDate.ToDateTime(TimeOnly.MinValue),
            nationalities: new List<Models.Nationality> { ParseEnum<Models.Nationality>(request.Nationality) },
            address: address,
            fatca: new Models.Fatca(status: false, confirmedAt: now.UtcDateTime),
            birthCity: request.City,
            birthCountry: ParseEnum<Models.BirthCountry>(request.Nationality),
            phoneNumber: string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber,
            termsAndConditions: new Models.TermsAndConditions(TermsAndConditionsDocumentId, now.UtcDateTime),
            dataPrivacyAndSharingAgreement: new Models.DataPrivacyAndSharingAgreement(DataPrivacyDocumentId, now.UtcDateTime)));

        var userResponse = await _client.UsersApi.CreateUserAsync(
            _upvestClientId, Guid.NewGuid(), Models.UpvestApiVersion.Enum1, userBody, cancellationToken);
        var userId = userResponse.Data.Match(byol => byol.Id, tol => tol.Id);

        // 2. KYC check. A confirmed address satisfies the proof-of-residency requirement too.
        var kyc = new Models.UserCheckKnowYourCustomerCreateRequest(
            type: "KYC",
            checkConfirmedAt: now.UtcDateTime,
            dataDownloadLink: KycEvidenceLink(),
            documentType: Models.DocumentType3.Passport,
            provider: "eShopOnWeb",
            method: Models.Method.ElectronicId,
            nationality: request.Nationality,
            confirmedAddress: address);
        await _client.UserChecksApi.CreateUserCheckAsync(
            userId, _upvestClientId, Models.UpvestApiVersion.Enum1,
            Containers.CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(kyc), cancellationToken);

        // 3. Instrument-fit (appropriateness) check. Best-effort: the shopper's fitness is also
        // acknowledged per-order (user_instrument_fit_acknowledgement), so a rejection of this
        // optional check must not abort onboarding.
        try
        {
            var instrumentFit = new Models.UserCheckInstrumentFitCreateRequest(
                type: "INSTRUMENT_FIT",
                checkConfirmedAt: now.UtcDateTime,
                instrumentSuitability: new Models.InstrumentSuitability(suitability: true));
            await _client.UserChecksApi.CreateUserCheckAsync(
                userId, _upvestClientId, Models.UpvestApiVersion.Enum1,
                Containers.CreateUserCheckBody.FromUserCheckInstrumentFitCreateRequest(instrumentFit), cancellationToken);
        }
        catch (UpvestInvestmentApi.Standard.Exceptions.ApiException)
        {
            // Continue onboarding; see note above.
        }

        // 4. Tax residency.
        var taxItem = string.IsNullOrWhiteSpace(request.TaxId)
            ? Models.Containers.TaxResidenciesSetRequestTaxResidencies.FromWithoutTaxIdentifierNumber(
                new Models.WithoutTaxIdentifierNumber(ParseEnum<Models.Country>(request.TaxCountry), Models.MissingTinReason.OtherReasons))
            : Models.Containers.TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
                new Models.WithTaxIdentifierNumber(ParseEnum<Models.Country>(request.TaxCountry), request.TaxId));
        await _client.TaxResidenciesApi.SetTaxResidenciesAsync(
            userId, _upvestClientId, Guid.NewGuid(), Models.UpvestApiVersion.Enum1,
            new Models.TaxResidenciesSetRequest(new List<Models.Containers.TaxResidenciesSetRequestTaxResidencies> { taxItem }),
            cancellationToken);

        // The account group and account cannot be created until the user has been accepted
        // (checks passed). Wait briefly for activation (Upvest activates asynchronously).
        await WaitForUserActiveAsync(userId, cancellationToken);

        // 5. Account group (PERSONAL) and 6. trading account. These are created via raw signed
        // JSON rather than the SDK: the generated account-group/account models expect a `users`
        // array, but this API returns a `user_id` field, so the typed responses fail to
        // deserialize. The endpoints, bodies and auth are exactly as the Upvest reference defines.
        var groupDoc = await RawSendAsync(HttpMethod.Post, "/account_groups",
            new { user_id = userId, type = "PERSONAL" }, cancellationToken);
        var accountGroupId = ReadGuid(groupDoc!, "id");

        var accountDoc = await RawSendAsync(HttpMethod.Post, "/accounts",
            new { user_id = userId, account_group_id = accountGroupId, type = "TRADING", name = "Invest your change" },
            cancellationToken);
        var accountId = ReadGuid(accountDoc!, "id");
        var accountStatus = MapAcceptance(ReadString(accountDoc!, "status"));

        return new UpvestEnrolmentResult(userId, accountGroupId, accountId, accountStatus);
    }

    public async Task<UpvestAcceptanceState> GetAccountAcceptanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        // Read account status via raw JSON (see the note in EnrolInvestorAsync).
        var doc = await RawSendAsync(HttpMethod.Get, $"/accounts/{accountId}", null, cancellationToken);
        return MapAcceptance(ReadString(doc!, "status"));
    }

    public async Task<UpvestPlacedOrder> PlaceInvestmentOrderAsync(Guid userId, Guid accountGroupId, Guid accountId, decimal amountEuros, CancellationToken cancellationToken = default)
    {
        var amount = amountEuros.ToString("0.00", CultureInfo.InvariantCulture);

        // Move the set-aside cash onto the shopper's Upvest account group so there are funds to
        // buy with. In the sandbox this is a virtual cash increase (VIRTUAL_CASH_INCREASE).
        await RawSendAsync(HttpMethod.Post, "/virtual_cash_balances/increases",
            new { account_group_id = accountGroupId, amount, currency = "EUR" }, cancellationToken);
        await WaitForCashAsync(accountGroupId, cancellationToken);

        // Placed via raw signed JSON: the SDK's Order model marks `fee` required, but this API
        // omits it on a fresh order, so the typed response fails to deserialize. Body and
        // endpoint are exactly the nominal MARKET BUY the Upvest reference defines.
        var body = new
        {
            user_id = userId,
            account_id = accountId,
            cash_amount = amount,
            currency = "EUR",
            side = "BUY",
            instrument_id = _settings.InstrumentId,
            instrument_id_type = "ISIN",
            order_type = "MARKET",
            user_instrument_fit_acknowledgement = true
        };
        var doc = await RawSendAsync(HttpMethod.Post, "/orders", body, cancellationToken);
        return new UpvestPlacedOrder(ReadGuid(doc!, "id"), _settings.InstrumentId, ReadString(doc!, "status"));
    }

    public async Task<UpvestInvestmentOutcome> GetOrderOutcomeAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var doc = await RawSendAsync(HttpMethod.Get, $"/orders/{orderId}", null, cancellationToken);
        if (System.Environment.GetEnvironmentVariable("UPVEST_DEBUG_RESPONSE") == "1")
        {
            var raw = doc!.RootElement.GetRawText();
            Console.Error.WriteLine($"[upvest-order] {raw[..Math.Min(500, raw.Length)]}");
        }
        var status = ReadString(doc!, "status").ToUpperInvariant();

        if (status == "CANCELLED")
        {
            return UpvestInvestmentOutcome.Failed;
        }

        // A settled execution is the strongest signal, where present.
        if (doc!.RootElement.TryGetProperty("executions", out var executions)
            && executions.ValueKind == JsonValueKind.Array)
        {
            foreach (var execution in executions.EnumerateArray())
            {
                if (execution.TryGetProperty("status", out var s)
                    && string.Equals(s.GetString(), "SETTLED", StringComparison.OrdinalIgnoreCase))
                {
                    return UpvestInvestmentOutcome.Settled;
                }
            }
        }

        // The order-level status enum has no SETTLED; FILLED is its terminal success state
        // (the buy executed and the shares are held), so it is the investment's settled outcome.
        if (status == "FILLED")
        {
            return UpvestInvestmentOutcome.Settled;
        }

        return UpvestInvestmentOutcome.Pending;
    }

    public async Task EnsureWebhookSubscriptionAsync(CancellationToken cancellationToken = default)
    {
        var url = WebhookUrl();
        if (url is null)
        {
            return;
        }

        var existing = await _client.WebhookSubscriptionsApi.ListWebhooksAsync(
            _upvestClientId, Models.UpvestApiVersion.Enum1, cancellationToken: cancellationToken);
        if (existing.Data?.Data?.Any(w => string.Equals(w.Url, url, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return;
        }

        var types = new List<Models.Type>
        {
            Models.Type.User, Models.Type.UserCheck, Models.Type.Account,
            Models.Type.AccountGroup, Models.Type.Order, Models.Type.Execution
        };
        var created = await _client.WebhookSubscriptionsApi.CreateWebhookAsync(
            _upvestClientId, Models.UpvestApiVersion.Enum1,
            new Models.WebhookCreateRequest("eShopOnWeb Invest", url, types), cancellationToken);

        // Subscriptions are created inactive; activate so events start flowing.
        await _client.WebhookSubscriptionsApi.UpdateWebhookAsync(
            created.Data.Id, _upvestClientId, Models.UpvestApiVersion.Enum1,
            new Models.WebhookUpdateRequest(title: "eShopOnWeb Invest", url: url, enabled: true, type: types),
            cancellationToken);
    }

    private string? _bearer;

    private async Task<string> BearerAsync()
    {
        if (_bearer is not null)
        {
            return _bearer;
        }
        var token = await _client.ClientCredentialsAuth.FetchTokenAsync();
        _bearer = token.AccessToken;
        return _bearer;
    }

    /// <summary>
    /// Sends a signed, bearer-authenticated request through the raw HTTP client and returns the
    /// parsed JSON. Used for the account-group/account endpoints the SDK cannot deserialize.
    /// Throws on a non-success status so onboarding surfaces the failure.
    /// </summary>
    private async Task<JsonDocument?> RawSendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(_raw.BaseUrl, path));
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + await BearerAsync());
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        using var response = await _raw.Http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (System.Environment.GetEnvironmentVariable("UPVEST_DEBUG_RESPONSE") == "1")
        {
            Console.Error.WriteLine($"[upvest-raw] {(int)response.StatusCode} {method.Method} {path}");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Upvest {method.Method} {path} returned {(int)response.StatusCode}.");
        }
        return JsonDocument.Parse(json);
    }

    private static Guid ReadGuid(JsonDocument doc, string property)
        => Guid.Parse(doc.RootElement.GetProperty(property).GetString()!);

    private static string ReadString(JsonDocument doc, string property)
        => doc.RootElement.TryGetProperty(property, out var e) ? e.GetString() ?? string.Empty : string.Empty;

    /// <summary>Waits briefly for a freshly-created virtual cash increase to be reflected as
    /// available cash on the account group, so the buy order has funds to settle against.</summary>
    private async Task WaitForCashAsync(Guid accountGroupId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 15; attempt++)
        {
            try
            {
                var doc = await RawSendAsync(HttpMethod.Get, $"/account_groups/{accountGroupId}/payments/cash_balances", null, cancellationToken);
                if (doc!.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in data.EnumerateArray())
                    {
                        if (entry.TryGetProperty("amount", out var amt)
                            && decimal.TryParse(amt.GetString(), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                            && value > 0m)
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Endpoint shape may vary; the short wait below still gives funding time to land.
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private async Task WaitForUserActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var debug = System.Environment.GetEnvironmentVariable("UPVEST_DEBUG_RESPONSE") == "1";
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                var response = await _client.UsersApi.RetrieveUserAsync(
                    userId, _upvestClientId, Models.UpvestApiVersion.Enum1, cancellationToken);
                var status = response.Data.Match(byol => byol.Status, tol => tol.Status);
                if (debug) Console.Error.WriteLine($"[upvest-user] attempt {attempt} status={status}");
                if (status == Models.Status.Active)
                {
                    return;
                }
            }
            catch (UpvestInvestmentApi.Standard.Exceptions.ApiException ex)
            {
                if (debug) Console.Error.WriteLine($"[upvest-user] attempt {attempt} retrieve failed: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private string KycEvidenceLink()
        => $"{(_settings.CallbackBaseUrl ?? string.Empty).TrimEnd('/')}/kyc-evidence/{Guid.NewGuid()}";

    private string? WebhookUrl()
    {
        if (string.IsNullOrWhiteSpace(_settings.CallbackBaseUrl))
        {
            return null;
        }
        return $"{_settings.CallbackBaseUrl.TrimEnd('/')}/api/investing/upvest-webhook";
    }

    private static UpvestAcceptanceState MapAcceptance(string status) => status?.ToUpperInvariant() switch
    {
        "ACTIVE" => UpvestAcceptanceState.Accepted,
        "CLOSING" or "CLOSED" or "LOCKED" => UpvestAcceptanceState.Rejected,
        _ => UpvestAcceptanceState.Pending
    };

    private static T ParseEnum<T>(string isoCode)
        => ApiHelper.JsonDeserialize<T>("\"" + isoCode + "\"");
}
