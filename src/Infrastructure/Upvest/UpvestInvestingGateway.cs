using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using APIMatic.Core.Utilities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Models = UpvestInvestmentApi.Standard.Models;
using Containers = UpvestInvestmentApi.Standard.Models.Containers;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Talks to Upvest through the generated SDK to carry out the investing feature's Upvest-side work:
/// onboarding a shopper as an investor, provisioning their investing account once accepted, placing
/// investment orders, and reporting order outcomes. Every call goes through the shared signing
/// <see cref="UpvestSigningHandler"/>.
/// </summary>
/// <remarks>
/// The account and account-group operations are issued as direct, signed HTTP calls rather than
/// through the SDK: the SDK's generated <c>Account</c>/<c>AccountGroup</c> models require a
/// <c>users</c> array that the Upvest sandbox does not return (it returns a scalar <c>user_id</c>),
/// so every SDK call for those resources fails to deserialize. The requests still follow the
/// SDK-documented contract, are signed by the shared handler, and use the SDK's OAuth token.
/// </remarks>
public class UpvestInvestingGateway : IUpvestInvestingGateway
{
    private const string InstrumentIdType = "ISIN";

    private readonly UpvestConnection _connection;
    private readonly UpvestSettings _settings;
    private readonly IAppLogger<UpvestInvestingGateway> _logger;
    private readonly Guid _upvestClientId;

    public UpvestInvestingGateway(
        UpvestConnection connection,
        UpvestSettings settings,
        IAppLogger<UpvestInvestingGateway> logger)
    {
        _connection = connection;
        _settings = settings;
        _logger = logger;
        _upvestClientId = Guid.Parse(settings.ClientId);
    }

    private static Models.UpvestApiVersion? ApiVersion => Models.UpvestApiVersion.Enum1;

    public async Task<UpvestEnrolmentResult> EnrolAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var userId = await CreateUserAsync(details, cancellationToken);
        await TrySetTaxResidencyAsync(userId, details, cancellationToken);
        await SubmitKycAsync(userId, details, cancellationToken);
        return await DetermineAcceptanceAsync(userId, null, cancellationToken);
    }

    public Task<UpvestEnrolmentResult> RefreshAcceptanceAsync(string upvestUserId, string? upvestAccountId, CancellationToken cancellationToken = default)
        => DetermineAcceptanceAsync(Guid.Parse(upvestUserId), upvestAccountId, cancellationToken);

    // Orders are placed and read via direct signed HTTP for the same reason as accounts: the SDK's
    // generated Order model requires fields the Upvest sandbox omits, so its response fails to
    // deserialize. The request still follows the SDK-documented order contract.
    public async Task<UpvestInvestmentResult> PlaceInvestmentAsync(string upvestUserId, string upvestAccountId, decimal amount, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync();
        var cash = amount.ToString("F2", CultureInfo.InvariantCulture);

        // Deposit the shopper's set-aside cash into their Upvest account so the buy can be funded.
        // Against the sandbox this is a virtual cash increase on the account group; in production it
        // would be a real transfer of the set-aside money.
        var groupId = await GetAccountGroupIdAsync(upvestUserId, upvestAccountId, token, cancellationToken);
        if (groupId != null)
        {
            var fundBody = $"{{\"account_group_id\":\"{groupId}\",\"amount\":\"{cash}\",\"currency\":\"EUR\"}}";
            using var _ = await RawPostAsync("/virtual_cash_balances/increases", fundBody, token, cancellationToken);
        }

        var body =
            $"{{\"account_id\":\"{upvestAccountId}\",\"user_id\":\"{upvestUserId}\"," +
            $"\"side\":\"BUY\",\"instrument_id\":\"{_settings.InstrumentId}\",\"instrument_id_type\":\"{InstrumentIdType}\"," +
            $"\"order_type\":\"MARKET\",\"cash_amount\":\"{cash}\",\"currency\":\"EUR\"}}";

        using var doc = await RawPostAsync("/orders", body, token, cancellationToken);
        var root = doc!.RootElement;
        var orderId = root.GetProperty("id").GetString()!;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
        return new UpvestInvestmentResult(orderId, _settings.InstrumentId, MapOrderState(status));
    }

    public async Task<UpvestInvestmentState> GetInvestmentStateAsync(string upvestOrderId, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync();
        using var doc = await RawGetAsync($"/orders/{upvestOrderId}", token, cancellationToken);
        var status = doc != null && doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
        return MapOrderState(status);
    }

    // ---- enrolment (SDK) ----

    private async Task<Guid> CreateUserAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken)
    {
        var address = new Models.Address(
            addressLine1: details.Address.Line1,
            postcode: details.Address.Postcode,
            country: ParseEnum<Models.Country>(details.Address.Country),
            city: details.Address.City);

        var request = new Models.UserTolCreateRequest(
            firstName: details.FirstName,
            lastName: details.LastName,
            email: details.Email,
            birthDate: details.BirthDate.ToDateTime(TimeOnly.MinValue),
            nationalities: new List<Models.Nationality> { ParseEnum<Models.Nationality>(details.Nationality) },
            address: address,
            fatca: new Models.Fatca(status: false, confirmedAt: DateTime.UtcNow))
        {
            PhoneNumber = string.IsNullOrWhiteSpace(details.PhoneNumber) ? null : details.PhoneNumber,
        };

        var body = Containers.CreateUserBody.FromUserTOLCreateRequest(request);
        var response = await _connection.Client.UsersApi.CreateUserAsync(
            _upvestClientId, Guid.NewGuid(), ApiVersion, body, cancellationToken);

        return response.Data.Match(userByol: u => u.Id, userTol: u => u.Id);
    }

    private async Task TrySetTaxResidencyAsync(Guid userId, InvestorEnrolmentDetails details, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(details.TaxId) || string.IsNullOrWhiteSpace(details.TaxCountry))
        {
            return;
        }

        try
        {
            var residency = Containers.TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
                new Models.WithTaxIdentifierNumber(
                    country: ParseEnum<Models.Country>(details.TaxCountry!),
                    taxIdentifierNumber: details.TaxId!));
            var body = new Models.TaxResidenciesSetRequest(new List<Containers.TaxResidenciesSetRequestTaxResidencies> { residency });
            await _connection.Client.TaxResidenciesApi.SetTaxResidenciesAsync(
                userId, _upvestClientId, Guid.NewGuid(), ApiVersion, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Setting tax residency failed ({Error}); continuing enrolment.", ex.GetType().Name);
        }
    }

    private async Task SubmitKycAsync(Guid userId, InvestorEnrolmentDetails details, CancellationToken cancellationToken)
    {
        try
        {
            var kyc = new Models.UserCheckKnowYourCustomerCreateRequest(
                type: "KYC",
                checkConfirmedAt: DateTime.UtcNow,
                dataDownloadLink: "https://eshoponweb.invest/kyc/" + userId,
                documentType: Models.DocumentType3.IdCard,
                provider: "eShopOnWeb",
                method: Models.Method.ElectronicId)
            {
                Nationality = details.Nationality,
            };
            var body = Containers.CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(kyc);
            await _connection.Client.UserChecksApi.CreateUserCheckAsync(userId, _upvestClientId, ApiVersion, body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Submitting KYC check failed ({Error}); acceptance will be reconciled later.", ex.GetType().Name);
        }
    }

    private async Task<UpvestEnrolmentResult> DetermineAcceptanceAsync(Guid userId, string? existingAccountId, CancellationToken cancellationToken)
    {
        var userResponse = await _connection.Client.UsersApi.RetrieveUserAsync(userId, _upvestClientId, ApiVersion, cancellationToken);
        var status = userResponse.Data.Match(userByol: u => u.Status, userTol: u => u.Status);

        if (status != Models.Status.Active)
        {
            return new UpvestEnrolmentResult(userId.ToString(), existingAccountId, UpvestAcceptanceStatus.Pending);
        }

        var accountId = existingAccountId ?? await TryProvisionAccountAsync(userId, cancellationToken);
        if (string.IsNullOrEmpty(accountId))
        {
            _logger.LogWarning("User {UserId} is accepted but their investing account is not ready yet.", userId);
            return new UpvestEnrolmentResult(userId.ToString(), null, UpvestAcceptanceStatus.Pending);
        }

        return new UpvestEnrolmentResult(userId.ToString(), accountId, UpvestAcceptanceStatus.Active);
    }

    // ---- account provisioning (direct signed HTTP; see class remarks) ----

    private async Task<string?> TryProvisionAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetAccessTokenAsync();

            using (var accounts = await RawGetAsync($"/users/{userId}/accounts", token, cancellationToken))
            {
                if (accounts != null && accounts.RootElement.TryGetProperty("data", out var data))
                {
                    var active = FindTradingAccount(data, "ACTIVE");
                    if (active != null)
                    {
                        return active;
                    }

                    if (FindTradingAccount(data, "PENDING_APPROVAL") != null)
                    {
                        return null; // on its way to being approved; reconcile later
                    }
                }
            }

            var groupId = await EnsureAccountGroupAsync(userId, token, cancellationToken);
            if (groupId == null)
            {
                return null;
            }

            var accountBody = $"{{\"user_id\":\"{userId}\",\"account_group_id\":\"{groupId}\",\"type\":\"TRADING\",\"name\":\"Spare change\"}}";
            using var created = await RawPostAsync("/accounts", accountBody, token, cancellationToken);

            // Newly created accounts start in PENDING_APPROVAL; a later reconcile returns it once ACTIVE.
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Provisioning investing account for user {UserId} failed ({Error}).", userId, ex.GetType().Name);
            return null;
        }
    }

    private async Task<string?> EnsureAccountGroupAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        using (var groups = await RawGetAsync($"/users/{userId}/account_groups", token, cancellationToken))
        {
            if (groups != null && groups.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                var active = data.EnumerateArray().FirstOrDefault(g =>
                    g.TryGetProperty("status", out var s) && s.GetString() == "ACTIVE");
                if (active.ValueKind == JsonValueKind.Object && active.TryGetProperty("id", out var existingId))
                {
                    return existingId.GetString();
                }
            }
        }

        var body = $"{{\"user_id\":\"{userId}\",\"type\":\"PERSONAL\"}}";
        using var createdGroup = await RawPostAsync("/account_groups", body, token, cancellationToken);
        if (createdGroup != null && createdGroup.RootElement.TryGetProperty("id", out var id))
        {
            return id.GetString();
        }

        return null;
    }

    private static string? FindTradingAccount(JsonElement data, string status)
    {
        if (data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var account in data.EnumerateArray())
        {
            if (account.TryGetProperty("type", out var type) && type.GetString() == "TRADING" &&
                account.TryGetProperty("status", out var s) && s.GetString() == status &&
                account.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }

        return null;
    }

    private async Task<string?> GetAccountGroupIdAsync(string userId, string accountId, string token, CancellationToken cancellationToken)
    {
        using var accounts = await RawGetAsync($"/users/{userId}/accounts", token, cancellationToken);
        if (accounts != null && accounts.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var account in data.EnumerateArray())
            {
                if (account.TryGetProperty("id", out var id) && id.GetString() == accountId &&
                    account.TryGetProperty("account_group_id", out var groupId))
                {
                    return groupId.GetString();
                }
            }
        }

        return null;
    }

    // ---- signed HTTP helpers ----

    private async Task<string> GetAccessTokenAsync()
    {
        var token = await _connection.Client.ClientCredentialsAuth.FetchTokenAsync();
        return token.AccessToken;
    }

    private async Task<JsonDocument?> RawGetAsync(string path, string token, CancellationToken cancellationToken)
        => await SendAsync(HttpMethod.Get, path, null, token, cancellationToken);

    private async Task<JsonDocument?> RawPostAsync(string path, string json, string token, CancellationToken cancellationToken)
        => await SendAsync(HttpMethod.Post, path, json, token, cancellationToken);

    private async Task<JsonDocument?> SendAsync(HttpMethod method, string path, string? json, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, "http://upvest" + path);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        request.Headers.TryAddWithoutValidation("upvest-client-id", _settings.ClientId);
        if (json != null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _connection.SignedHttpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Upvest {method} {path} returned {(int)response.StatusCode}.");
        }

        return string.IsNullOrWhiteSpace(body) ? null : JsonDocument.Parse(body);
    }

    // Upvest order status: NEW / PROCESSING are in-flight, FILLED is settled, CANCELLED is failed.
    private static UpvestInvestmentState MapOrderState(string? status) => status switch
    {
        "FILLED" => UpvestInvestmentState.Settled,
        "CANCELLED" => UpvestInvestmentState.Failed,
        _ => UpvestInvestmentState.Pending,
    };

    private static T ParseEnum<T>(string wireValue)
        => CoreHelper.JsonDeserialize<T>("\"" + wireValue + "\"");
}
