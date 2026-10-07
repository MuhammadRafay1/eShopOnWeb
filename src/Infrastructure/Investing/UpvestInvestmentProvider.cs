using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using UpvestInvestmentApi.Standard.Exceptions;
using Models = UpvestInvestmentApi.Standard.Models;
using Containers = UpvestInvestmentApi.Standard.Models.Containers;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Adapts the application's <see cref="IInvestmentProvider"/> port onto the
/// Upvest Investment API SDK: creating an investor (user), recording their tax
/// residency, provisioning a trading account to hold investments, placing BUY
/// orders for the configured fund, and reading back order outcomes. No personal
/// details are logged.
/// </summary>
public sealed class UpvestInvestmentProvider : IInvestmentProvider
{
    private readonly UpvestClient _client;
    private readonly UpvestSettings _settings;
    private readonly IAppLogger<UpvestInvestmentProvider> _logger;

    private UpvestInvestmentApi.Standard.UpvestInvestmentApiClient Api => _client.Api;
    private static Guid ClientId => UpvestClient.HeaderClientId;

    public UpvestInvestmentProvider(
        UpvestClient client,
        IOptions<UpvestSettings> settings,
        IAppLogger<UpvestInvestmentProvider> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ProviderEnrolment> EnrolAsync(InvestorRegistration registration, CancellationToken cancellationToken = default)
    {
        var body = BuildCreateUserBody(registration);
        var created = await Api.UsersApi.CreateUserAsync(ClientId, Guid.NewGuid(), null, body);
        var (userId, userStatus) = created.Data.Match(
            byol => (byol.Id, byol.Status),
            tol => (tol.Id, tol.Status));

        _logger.LogInformation("Created Upvest user {UserId} with status {Status}.", userId, userStatus);

        // Forward the shopper's tax residency. Informational KYC data: a failure here
        // must not abort enrolment, so it is recorded best-effort.
        await TrySetTaxResidencyAsync(userId, registration.TaxId, registration.TaxCountry);

        // Record the identity check that moves the user towards acceptance (activation).
        await TryCreateKycCheckAsync(userId, registration);

        // Re-read the user: acceptance may already have completed, in which case we
        // can provision the holding account straight away.
        return await RefreshEnrolmentAsync(userId.ToString(), providerAccountId: null, cancellationToken);
    }

    public async Task<ProviderEnrolment> RefreshEnrolmentAsync(string providerUserId, string? providerAccountId, CancellationToken cancellationToken = default)
    {
        var userId = Guid.Parse(providerUserId);
        var user = await Api.UsersApi.RetrieveUserAsync(userId, ClientId, null);
        var userStatus = user.Data.Match(byol => byol.Status, tol => tol.Status);

        return await ResolveEnrolmentAsync(userId, userStatus, providerAccountId, cancellationToken);
    }

    public async Task<string> InvestAsync(string providerUserId, string providerAccountId, decimal amountEuros, CancellationToken cancellationToken = default)
    {
        var accountId = Guid.Parse(providerAccountId);
        var userId = Guid.Parse(providerUserId);
        var clientReference = Guid.NewGuid().ToString("N");

        // The shopper's set-aside change funds the purchase: credit the account group with
        // the cash to be invested before placing the buy, otherwise the order is cancelled
        // for want of funds.
        await FundAccountGroupAsync(userId, amountEuros);

        var body = new Models.OrderPlaceRequest
        {
            AccountId = accountId,
            UserId = Guid.Parse(providerUserId),
            Side = Models.Side.Buy,
            InstrumentId = _settings.InstrumentId,
            InstrumentIdType = "ISIN",
            CashAmount = amountEuros.ToString("F2", CultureInfo.InvariantCulture),
            Currency = Models.Currency29.Eur,
            OrderType = Models.OrderType.Market,
            UserInstrumentFitAcknowledgement = true,
            ClientReference = clientReference,
        };

        // A freshly placed order has no fee yet, which the generated Order39 model requires,
        // so the typed response fails to deserialize. The order is still placed; read its id
        // back raw, correlating on the client reference we set.
        await TolerateUnionResponseAsync(() => Api.OrdersApi.PlaceOrderAsync(ClientId, Guid.NewGuid(), null, body));

        var orderId = await FindOrderIdAsync(accountId, clientReference)
            ?? throw new InvalidOperationException("Order was placed but its id could not be read back.");

        _logger.LogInformation("Placed Upvest order {OrderId} for {Amount} EUR.", orderId, body.CashAmount);
        return orderId;
    }

    public async Task<ProviderInvestmentOutcome> GetInvestmentOutcomeAsync(string providerOrderId, CancellationToken cancellationToken = default)
    {
        // Read the order raw: a NEW order omits the Order39-required 'fee', so the typed call throws.
        var json = await _client.GetJsonAsync($"/orders/{providerOrderId}");
        return (json?["status"]?.ToString()) switch
        {
            "FILLED" => ProviderInvestmentOutcome.Settled,
            "CANCELLED" => ProviderInvestmentOutcome.Failed,
            _ => ProviderInvestmentOutcome.Pending,
        };
    }

    public async Task RegisterCallbackAsync(string callbackUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            return;
        }

        try
        {
            var body = new Models.WebhookCreateRequest
            {
                Title = "eShopOnWeb invest-your-change",
                Url = callbackUrl,
                Type = new List<Models.Type> { Models.Type.Order, Models.Type.Execution },
            };

            await Api.WebhookSubscriptionsApi.CreateWebhookAsync(ClientId, null, body);
            _logger.LogInformation("Registered Upvest webhook callback at {Url}.", callbackUrl);
        }
        catch (Exception ex)
        {
            // Settlement also reconciles on read; a failed subscription must not break anything.
            _logger.LogWarning("Upvest webhook callback was not registered: {Error}", ex.Message);
        }
    }

    private async Task FundAccountGroupAsync(Guid userId, decimal amountEuros)
    {
        var groupId = await FindAccountGroupIdAsync(userId)
            ?? throw new InvalidOperationException("No account group to fund.");

        var body = new Models.VirtualCashBalanceVirtualCashIncreaseCreateRequest
        {
            AccountGroupId = groupId,
            Amount = amountEuros.ToString("F2", CultureInfo.InvariantCulture),
            Currency = Models.Currency1.Eur,
        };

        await TolerateUnionResponseAsync(() => Api.VirtualCashBalancesApi.CreateVirtualCashIncreaseAsync(ClientId, Guid.NewGuid(), null, body));
        _logger.LogInformation("Credited account group {GroupId} with {Amount} EUR.", groupId, body.Amount);
    }

    private async Task<string?> FindOrderIdAsync(Guid accountId, string clientReference)
    {
        var json = await _client.GetJsonAsync($"/accounts/{accountId}/orders?order=DESC&limit=100");
        if (json?["data"] is not JArray data || data.Count == 0)
        {
            return null;
        }

        var match = data.FirstOrDefault(o => o["client_reference"]?.ToString() == clientReference) ?? data[0];
        return match["id"]?.ToString();
    }

    // --- enrolment helpers -------------------------------------------------

    private async Task<ProviderEnrolment> ResolveEnrolmentAsync(Guid userId, Models.Status userStatus, string? providerAccountId, CancellationToken ct)
    {
        // A user Upvest has off-boarded can never invest. A freshly created user is
        // INACTIVE — that is "not yet accepted" (pending), not a rejection.
        if (userStatus is Models.Status.Offboarding or Models.Status.Offboarded)
        {
            return new ProviderEnrolment(userId.ToString(), providerAccountId, EnrolmentStatus.Rejected);
        }

        // Already have an account: report its current state.
        if (!string.IsNullOrEmpty(providerAccountId))
        {
            var status = await GetAccountStatusAsync(userId, Guid.Parse(providerAccountId!));
            return new ProviderEnrolment(userId.ToString(), providerAccountId, status ?? EnrolmentStatus.Pending);
        }

        // User is accepted but has no holding account yet: provision one.
        if (userStatus == Models.Status.Active)
        {
            var provisioned = await TryProvisionAccountAsync(userId, ct);
            if (provisioned is { } account)
            {
                return new ProviderEnrolment(userId.ToString(), account.AccountId, account.Status);
            }
        }

        // Accepted by Upvest but the holding account is not ready yet.
        return new ProviderEnrolment(userId.ToString(), null, EnrolmentStatus.Pending);
    }

    private async Task<(string AccountId, EnrolmentStatus Status)?> TryProvisionAccountAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var groupId = await EnsureAccountGroupAsync(userId);

            var account = await FindAccountAsync(userId);
            if (account is null)
            {
                var request = Containers.CreateAccountBody.FromAccountCreateUserRequest(
                    new Models.AccountCreateUserRequest
                    {
                        UserId = userId,
                        AccountGroupId = groupId,
                        Type = Models.Type16.Trading,
                        Name = "Spare change investments",
                    });

                // The create response is a strict one-of the SDK cannot deserialize when
                // the variants overlap; the account is still created server-side, so we
                // tolerate that and read it back raw.
                await TolerateUnionResponseAsync(() => Api.AccountsApi.CreateAccountAsync(ClientId, Guid.NewGuid(), null, request));
                account = await FindAccountAsync(userId);
            }

            if (account is not { } created)
            {
                return null;
            }

            _logger.LogInformation("Provisioned Upvest account {AccountId} ({Status}) for user {UserId}.",
                created.Id, created.Status, userId);
            return (created.Id.ToString(), created.Status);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Could not provision an account for user {UserId} yet: {Detail}", userId, Describe(ex));
            return null;
        }
        catch (Exception ex)
        {
            // Provisioning trouble (including SDK response-deserialization quirks) must not
            // crash enrolment; leave it pending and let a later refresh retry.
            _logger.LogWarning("Account provisioning for user {UserId} did not complete: {Error}", userId, ex.Message);
            return null;
        }
    }

    private async Task<Guid> EnsureAccountGroupAsync(Guid userId)
    {
        var existing = await FindAccountGroupIdAsync(userId);
        if (existing is { } groupId)
        {
            return groupId;
        }

        var body = Containers.CreateAccountGroupBody.FromAccountGroupCreateUserRequest(
            new Models.AccountGroupCreateUserRequest
            {
                UserId = userId,
                Type = Models.Type13.Personal,
            });

        // Same strict one-of situation as accounts: tolerate the undeserializable body and re-read.
        await TolerateUnionResponseAsync(() => Api.AccountGroupsApi.CreateAccountGroupAsync(ClientId, Guid.NewGuid(), null, body));

        return await FindAccountGroupIdAsync(userId)
            ?? throw new InvalidOperationException("Account group could not be provisioned.");
    }

    // The account-group list/retrieve models reject the live payload, so read the id raw.
    private async Task<Guid?> FindAccountGroupIdAsync(Guid userId)
    {
        var json = await _client.GetJsonAsync($"/users/{userId}/account_groups");
        var first = (json?["data"] as JArray)?.FirstOrDefault();
        var id = first?["id"]?.ToString();
        return Guid.TryParse(id, out var groupId) ? groupId : null;
    }

    // The account list model can also reject the live payload once populated, so read it raw.
    private async Task<(Guid Id, EnrolmentStatus Status)?> FindAccountAsync(Guid userId)
    {
        var json = await _client.GetJsonAsync($"/users/{userId}/accounts");
        if (json?["data"] is not JArray data || data.Count == 0)
        {
            return null;
        }

        var account = data.FirstOrDefault(a => (a["type"]?.ToString()) == "TRADING") ?? data[0];
        if (!Guid.TryParse(account["id"]?.ToString(), out var accountId))
        {
            return null;
        }

        return (accountId, MapAccountStatus(account["status"]?.ToString()));
    }

    private async Task<EnrolmentStatus?> GetAccountStatusAsync(Guid userId, Guid accountId)
    {
        var account = await FindAccountAsync(userId);
        return account is { } a && a.Id == accountId ? a.Status : account?.Status;
    }

    /// <summary>
    /// Runs a create call whose response is a strict one-of the SDK cannot deserialize
    /// when its variants overlap. A real API error (<see cref="ApiException"/>) still
    /// propagates; only the post-success deserialization failure is swallowed, after
    /// which the caller reads the created resource back from a concrete list endpoint.
    /// </summary>
    private static async Task TolerateUnionResponseAsync(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (ApiException)
        {
            throw;
        }
        catch
        {
            // Resource created; response body did not match the generated one-of. Ignore.
        }
    }

    private async Task TryCreateKycCheckAsync(Guid userId, InvestorRegistration r)
    {
        try
        {
            var kyc = new Models.UserCheckKnowYourCustomerCreateRequest
            {
                Type = "KYC",
                CheckConfirmedAt = DateTime.UtcNow,
                DataDownloadLink = $"{_settings.CallbackBaseUrl}/kyc-evidence/{userId}",
                DocumentType = Models.DocumentType3.IdCard,
                Provider = "eShopOnWeb",
                Method = Models.Method.ElectronicId,
            };

            if (TryParseCountry(r.Address.Country, out var country))
            {
                kyc.ConfirmedAddress = new Models.Address
                {
                    AddressLine1 = r.Address.Line1,
                    Postcode = r.Address.Postcode,
                    City = r.Address.City,
                    Country = country,
                };
            }

            var body = Containers.CreateUserCheckBody.FromUserCheckKnowYourCustomerCreateRequest(kyc);
            await Api.UserChecksApi.CreateUserCheckAsync(userId, ClientId, null, body);
            _logger.LogInformation("Recorded KYC check for user {UserId}.", userId);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Could not record KYC check for user {UserId}: {Detail}", userId, Describe(ex));
        }
    }

    private static string Describe(ApiException ex) =>
        ex is ErrorException e ? $"{e.Status} {e.Title}: {e.Detail}" : $"{ex.ResponseCode} {ex.Message}";

    private async Task TrySetTaxResidencyAsync(Guid userId, string taxId, string taxCountry)
    {
        if (string.IsNullOrWhiteSpace(taxId) || !TryParseCountry(taxCountry, out var country))
        {
            return;
        }

        try
        {
            var body = new Models.TaxResidenciesSetRequest
            {
                TaxResidencies = new List<Containers.TaxResidenciesSetRequestTaxResidencies>
                {
                    Containers.TaxResidenciesSetRequestTaxResidencies.FromWithTaxIdentifierNumber(
                        new Models.WithTaxIdentifierNumber
                        {
                            Country = country,
                            TaxIdentifierNumber = taxId,
                        }),
                },
            };

            await Api.TaxResidenciesApi.SetTaxResidenciesAsync(userId, ClientId, Guid.NewGuid(), null, body);
            _logger.LogInformation("Recorded tax residency for user {UserId}.", userId);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Could not record tax residency for user {UserId} (status {Code}).", userId, ex.ResponseCode);
        }
    }

    // --- mapping -----------------------------------------------------------

    private static EnrolmentStatus MapAccountStatus(string? status) => status switch
    {
        "ACTIVE" => EnrolmentStatus.Active,
        "PENDING_APPROVAL" => EnrolmentStatus.Pending,
        null or "" => EnrolmentStatus.Pending,
        _ => EnrolmentStatus.Rejected, // CLOSING / CLOSED / LOCKED
    };

    private Containers.CreateUserBody BuildCreateUserBody(InvestorRegistration r)
    {
        var address = new Models.Address
        {
            AddressLine1 = r.Address.Line1,
            Postcode = r.Address.Postcode,
            City = r.Address.City,
            Country = ParseCountry(r.Address.Country),
        };

        var request = new Models.UserTolCreateRequest
        {
            FirstName = r.FirstName,
            LastName = r.LastName,
            Email = r.Email,
            BirthDate = r.BirthDate,
            Nationalities = new List<Models.Nationality> { ParseNationality(r.Nationality) },
            Address = address,
            PhoneNumber = r.PhoneNumber,
            Fatca = new Models.Fatca(status: false, confirmedAt: DateTime.UtcNow),
        };

        return Containers.CreateUserBody.FromUserTOLCreateRequest(request);
    }

    private static Models.Country ParseCountry(string code) =>
        TryParseCountry(code, out var country)
            ? country
            : throw new ArgumentException($"Unsupported country code '{code}'.");

    private static bool TryParseCountry(string code, out Models.Country country) =>
        Enum.TryParse(code, ignoreCase: true, out country);

    private static Models.Nationality ParseNationality(string code) =>
        Enum.TryParse<Models.Nationality>(code, ignoreCase: true, out var nationality)
            ? nationality
            : throw new ArgumentException($"Unsupported nationality code '{code}'.");
}
