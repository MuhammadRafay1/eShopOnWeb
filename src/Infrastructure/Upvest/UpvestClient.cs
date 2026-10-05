using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Core.Hooks;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.AccountGroups;
using UpvestInvestmentApi.Requests.AccountsApi;
using UpvestInvestmentApi.Requests.Orders;
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.TopUps;
using UpvestInvestmentApi.Requests.UserChecks;
using UpvestInvestmentApi.Requests.Users;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The Upvest gateway, implemented over the Upvest SDK. The SDK builds, signs (via
/// <see cref="UpvestAuthHandler"/>) and sends every request; this class reads each response's raw body
/// through a per-call response hook, so a provider that returns leaner payloads than the SDK's strict models
/// does not break the flow. Every call is bounded by a total time budget, no personal data is ever logged,
/// and every failure is translated into <see cref="UpvestIntegrationException"/> with a caller-safe message.
/// </summary>
public sealed class UpvestClient : IUpvestInvestorGateway
{
    // Auth members are injected by the delegating handler; these non-empty placeholders only satisfy the
    // SDK's header validation and are overwritten on the wire by UpvestAuthHandler.
    private static readonly Guid NoClientId = Guid.Empty;
    private const string NoAuth = "placeholder";
    private const string NoBearer = "Bearer placeholder";

    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestSettings _settings;

    public UpvestClient(UpvestInvestmentApiClient client, UpvestSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public async Task<Guid> EnrolInvestorAsync(EnrolmentForm form, CancellationToken cancellationToken)
    {
        if (!Nationality.TryGetKnownValue(form.Nationality.ToUpperInvariant(), out var nationality))
            throw new UpvestIntegrationException($"Nationality '{form.Nationality}' is not a recognised ISO 3166-1 alpha-2 code.", HttpStatusCode.BadRequest);
        if (!Country.TryGetKnownValue(form.Address.Country.ToUpperInvariant(), out var country))
            throw new UpvestIntegrationException($"Country '{form.Address.Country}' is not a recognised ISO 3166-1 alpha-2 code.", HttpStatusCode.BadRequest);
        if (!Country.TryGetKnownValue(form.TaxCountry.ToUpperInvariant(), out var taxCountry))
            throw new UpvestIntegrationException($"Tax country '{form.TaxCountry}' is not a recognised ISO 3166-1 alpha-2 code.", HttpStatusCode.BadRequest);

        var now = DateTimeOffset.UtcNow;

        // 1. Create the Upvest user from the personal details.
        var tol = new UserTolCreateRequest
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            Email = form.Email,
            BirthDate = new DateTimeOffset(form.BirthDate.Year, form.BirthDate.Month, form.BirthDate.Day, 0, 0, 0, TimeSpan.Zero),
            Nationalities = new[] { nationality },
            Address = new Address
            {
                AddressLine1 = form.Address.Line1,
                Postcode = form.Address.Postcode,
                City = form.Address.City,
                Country = country
            },
            PhoneNumber = string.IsNullOrWhiteSpace(form.PhoneNumber) ? null : form.PhoneNumber,
            Fatca = new Fatca { Status = false, ConfirmedAt = now }
        };

        var user = await SendAsync("create user", (ro, ct) => _client.Users.CreateUser(new CreateUserRequest
        {
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth,
            IdempotencyKey = Guid.NewGuid(),
            Body = UserCreateRequest.UserTolCreateRequest(tol)
        }, ro, ct), cancellationToken);
        var userId = ReadGuid(user, "id");

        // 2. Submit the KYC check (required before the user can be activated).
        var kyc = new UserCheckKnowYourCustomerCreateRequest
        {
            CheckConfirmedAt = now,
            DataDownloadLink = "https://files.eshoponweb.example/kyc-evidence.pdf",
            DocumentType = DocumentType3.TryGetKnownValue("PASSPORT", out var doc) ? doc : throw Unexpected("document type"),
            Provider = "eShopOnWeb",
            Method = Method.TryGetKnownValue("VIDEO_ID", out var method) ? method : throw Unexpected("verification method"),
            Nationality = form.Nationality.ToUpperInvariant()
        };
        await SendAsync("create user check", (ro, ct) => _client.UserChecks.CreateUserCheck(new CreateUserCheckRequest
        {
            UserId = userId,
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth,
            Body = UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(kyc)
        }, ro, ct), cancellationToken);

        // 3. Record tax residency (also required before activation).
        var taxBody = new TaxResidenciesSetRequest
        {
            TaxResidencies = new TaxResidencyForCreateRequest[]
            {
                TaxResidencyForCreateRequest.WithTaxIdentifierNumber(new WithTaxIdentifierNumber
                {
                    Country = taxCountry,
                    TaxIdentifierNumber = form.TaxId
                })
            }
        };
        await SendAsync("set tax residencies", (ro, ct) => _client.TaxResidencies.SetTaxResidencies(new SetTaxResidenciesRequest
        {
            UserId = userId,
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth,
            IdempotencyKey = Guid.NewGuid(),
            Body = taxBody
        }, ro, ct), cancellationToken);

        return userId;
    }

    public async Task<InvestorStatus> GetAcceptanceStatusAsync(Guid upvestUserId, CancellationToken cancellationToken)
    {
        var user = await SendAsync("retrieve user", (ro, ct) => _client.Users.RetrieveUser(new RetrieveUserRequest
        {
            UserId = upvestUserId,
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth
        }, ro, ct), cancellationToken);

        return (ReadString(user, "status") ?? "") switch
        {
            "ACTIVE" => InvestorStatus.Active,
            "OFFBOARDING" or "OFFBOARDED" => InvestorStatus.Rejected,
            _ => InvestorStatus.Pending
        };
    }

    public async Task<Guid?> TryResolveInvestmentAccountAsync(Guid upvestUserId, CancellationToken cancellationToken)
    {
        var accounts = await SendAsync("list user accounts", (ro, ct) => _client.AccountsApi.ListUserAccounts(new ListUserAccountsRequest
        {
            UserId = upvestUserId,
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth
        }, ro, ct), cancellationToken);

        var data = Data(accounts);
        var active = data.FirstOrDefault(a => ReadString(a, "status") == "ACTIVE" && ReadString(a, "type") == "TRADING");
        if (active.ValueKind != JsonValueKind.Object)
            active = data.FirstOrDefault(a => ReadString(a, "status") == "ACTIVE");
        if (active.ValueKind == JsonValueKind.Object) return ReadGuid(active, "id");

        // An account exists but is still being approved — wait for it rather than creating another.
        if (data.Any(a => ReadString(a, "type") == "TRADING")) return null;

        var groupId = await EnsureAccountGroupAsync(upvestUserId, cancellationToken);
        await SendAsync("create account", (ro, ct) => _client.AccountsApi.CreateAccount(new CreateAccountRequest
        {
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth,
            IdempotencyKey = Guid.NewGuid(),
            Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
            {
                UserId = upvestUserId, AccountGroupId = groupId, Type = Type16.Trading, Name = "Invest your change"
            })
        }, ro, ct), cancellationToken);

        // The account is created PENDING_APPROVAL and becomes active shortly; resolve it on a later attempt.
        return null;
    }

    private async Task<Guid> EnsureAccountGroupAsync(Guid upvestUserId, CancellationToken cancellationToken)
    {
        var groups = await SendAsync("list user account groups", (ro, ct) => _client.AccountGroups.ListUserAccountGroups(new ListUserAccountGroupsRequest
        {
            UserId = upvestUserId,
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth
        }, ro, ct), cancellationToken);

        var data = Data(groups);
        var existing = data.FirstOrDefault(g => ReadString(g, "status") == "ACTIVE");
        if (existing.ValueKind == JsonValueKind.Object) return ReadGuid(existing, "id");

        var createdGroup = await SendAsync("create account group", (ro, ct) => _client.AccountGroups.CreateAccountGroup(new CreateAccountGroupRequest
        {
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth,
            IdempotencyKey = Guid.NewGuid(),
            Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
            {
                UserId = upvestUserId, Type = Type13.Personal
            })
        }, ro, ct), cancellationToken);

        return ReadGuid(createdGroup, "id");
    }

    public async Task<UpvestInvestmentPlacement> PlaceInvestmentAsync(Guid upvestAccountId, decimal amountInEuros, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var cashAmount = amountInEuros.ToString("0.00", CultureInfo.InvariantCulture);

        // Fund the account group with the set-aside cash so the buy can be filled, then place the order.
        var account = await SendAsync("retrieve account", (ro, ct) => _client.AccountsApi.RetrieveAccount(new UpvestInvestmentApi.Requests.AccountsApi.RetrieveAccountRequest
        {
            AccountId = upvestAccountId,
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth
        }, ro, ct), cancellationToken);
        var accountGroupId = ReadGuid(account, "account_group_id");

        if (!Currency.TryGetKnownValue("EUR", out var currency)) throw Unexpected("currency");
        await SendAsync("top up", (ro, ct) => _client.TopUps.CreateTopup(new CreateTopupRequest
        {
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth,
            // Distinct from the order's key: the provider's idempotency store is shared across write endpoints.
            IdempotencyKey = Guid.NewGuid(),
            Body = new PaymentsTopUpCreateRequest
            {
                AccountGroupId = accountGroupId,
                CashAmount = cashAmount,
                Currency = currency
            }
        }, ro, ct), cancellationToken);

        var body = new OrderPlaceRequest
        {
            AccountId = upvestAccountId,
            Side = Side.Buy,
            InstrumentId = _settings.InstrumentId,
            CashAmount = cashAmount
        };
        if (Currency29.TryGetKnownValue("EUR", out var eur))
        {
            body = body with { Currency = eur };
        }

        var order = await SendAsync("place order", (ro, ct) => _client.Orders.PlaceOrder(new PlaceOrderRequest
        {
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth,
            IdempotencyKey = idempotencyKey,
            Body = body
        }, ro, ct), cancellationToken);

        return new UpvestInvestmentPlacement(ReadGuid(order, "id"), MapOrderStatus(ReadString(order, "status")));
    }

    public async Task<InvestmentStatus> GetInvestmentOutcomeAsync(Guid upvestOrderId, CancellationToken cancellationToken)
    {
        var order = await SendAsync("retrieve order", (ro, ct) => _client.Orders.RetrieveOrder(new RetrieveOrderRequest
        {
            OrderId = upvestOrderId,
            UpvestClientId = NoClientId, Authorization = NoBearer, Signature = NoAuth, SignatureInput = NoAuth
        }, ro, ct), cancellationToken);

        return MapOrderStatus(ReadString(order, "status"));
    }

    private static InvestmentStatus MapOrderStatus(string? status) => status switch
    {
        "FILLED" => InvestmentStatus.Settled,
        "CANCELLED" => InvestmentStatus.Failed,
        _ => InvestmentStatus.Pending
    };

    // --- response reading helpers (tolerate leaner-than-model payloads) ---

    private static IReadOnlyList<JsonElement> Data(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToList()
            : Array.Empty<JsonElement>();

    private static string? ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Guid ReadGuid(JsonElement element, string property)
    {
        var raw = ReadString(element, property);
        if (raw is not null && Guid.TryParse(raw, out var id)) return id;
        throw new UpvestIntegrationException($"Upvest response did not contain a usable '{property}'.");
    }

    private static UpvestIntegrationException Unexpected(string what) =>
        new($"The Upvest SDK does not declare the expected {what} value.");

    /// <summary>
    /// Runs an SDK call, captures the raw response via a per-call hook, and returns the parsed response JSON.
    /// A 2xx whose body does not satisfy the SDK's strict model is treated as success (the body is still read);
    /// a non-2xx, a transport failure or a timeout becomes a translated <see cref="UpvestIntegrationException"/>.
    /// </summary>
    private static async Task<JsonElement> SendAsync(string operation, Func<RequestOptions, CancellationToken, Task> call, CancellationToken cancellationToken)
    {
        var capture = new Capture();
        var options = new RequestOptions
        {
            Hooks = new[]
            {
                SdkHook.OnResponse(async (response, _, ct) =>
                {
                    capture.Status = (int)response.StatusCode;
                    capture.Body = await response.Content.ReadAsStringAsync(ct);
                })
            }
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);

        try
        {
            await call(options, cts.Token);
        }
        catch (ResponseDeserializationException)
        {
            // A body that did not fit the SDK model. The captured status below decides success vs failure.
        }
        catch (ApiException ex)
        {
            throw new UpvestIntegrationException($"Upvest returned HTTP {(int)ex.StatusCode} for {operation}.", ex.StatusCode, ex);
        }
        catch (AuthSchemeException ex)
        {
            throw new UpvestIntegrationException("Upvest credentials could not be applied.", null, ex);
        }
        catch (SdkTimeoutException ex)
        {
            throw new UpvestIntegrationException($"Upvest did not respond in time for {operation}.", null, ex);
        }
        catch (SdkConnectionException ex)
        {
            throw new UpvestIntegrationException($"Upvest could not be reached for {operation}.", null, ex);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpvestIntegrationException($"The Upvest call '{operation}' exceeded its time budget.");
        }

        if (capture.Status == 0)
            throw new UpvestIntegrationException($"Upvest returned no response for {operation}.");
        if (capture.Status is < 200 or >= 300)
            throw new UpvestIntegrationException($"Upvest returned HTTP {capture.Status} for {operation}.", (HttpStatusCode)capture.Status);

        return ParseBody(capture.Body);
    }

    private static JsonElement ParseBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return default;
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private sealed class Capture
    {
        public int Status;
        public string Body = string.Empty;
    }
}
