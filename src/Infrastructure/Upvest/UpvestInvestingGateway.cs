using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.AccountGroups;
using UpvestInvestmentApi.Requests.AccountsApi;
using UpvestInvestmentApi.Requests.Orders;
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.UserChecks;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.VirtualCashBalances;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Implements the application's Upvest conversation against the SDK. Every call is built with the SDK's request
/// models and sent through the SDK client (so the one signing handler authenticates it); the response is read
/// from the captured raw JSON by its documented wire field names, because the provider returns success bodies
/// that omit members the generated response models mark <c>required</c>. Owns the error boundary: every SDK
/// failure becomes a <see cref="UpvestGatewayException"/>.
/// </summary>
public sealed class UpvestInvestingGateway : IUpvestInvestingGateway
{
    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);

    // The signing handler overwrites these; the SDK only requires the header members to be non-empty.
    private const string AuthPlaceholder = "Bearer placeholder";
    private const string SigPlaceholder = "placeholder";

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestResponseCapture _capture;
    private readonly UpvestSettings _settings;
    private readonly TimeProvider _clock;
    private readonly Guid _clientId;

    public UpvestInvestingGateway(UpvestInvestmentApiClient client, UpvestResponseCapture capture, IOptions<UpvestSettings> options, TimeProvider clock)
    {
        _client = client;
        _capture = capture;
        _settings = options.Value;
        _clock = clock;
        _clientId = Guid.Parse(_settings.ClientId);
    }

    public async Task<Guid> CreateInvestorAsync(InvestorSignUp signUp, string idempotencyScope, CancellationToken cancellationToken)
    {
        var nationality = ResolveNationality(signUp.Nationality);
        var country = ResolveCountry(signUp.Address.Country);
        var taxCountry = ResolveCountry(signUp.TaxCountry);

        var userBody = UserCreateRequest.UserTolCreateRequest(new UserTolCreateRequest
        {
            FirstName = signUp.FirstName,
            LastName = signUp.LastName,
            Email = signUp.Email,
            BirthDate = signUp.BirthDate,
            Nationalities = new[] { nationality },
            Address = new Address
            {
                AddressLine1 = signUp.Address.Line1,
                Postcode = signUp.Address.Postcode,
                City = signUp.Address.City,
                Country = country,
            },
            Fatca = new Fatca { Status = false, ConfirmedAt = _clock.GetUtcNow() },
            PhoneNumber = string.IsNullOrWhiteSpace(signUp.PhoneNumber) ? null : signUp.PhoneNumber,
        });

        var created = await CallAsync("create user", isWrite: true, ct => _client.Users.CreateUser(new CreateUserRequest
        {
            UpvestClientId = _clientId,
            Authorization = AuthPlaceholder,
            Signature = SigPlaceholder,
            SignatureInput = SigPlaceholder,
            IdempotencyKey = StableGuid(idempotencyScope, "user"),
            Body = userBody,
        }, cancellationToken: ct), cancellationToken);

        var userId = GetGuid(created, "id");

        // Identity (KYC) check — part of onboarding so Upvest can accept the shopper. Response not consumed.
        await CallAsync("create user check", isWrite: true, ct => _client.UserChecks.CreateUserCheck(new CreateUserCheckRequest
        {
            UserId = userId,
            UpvestClientId = _clientId,
            Authorization = AuthPlaceholder,
            Signature = SigPlaceholder,
            SignatureInput = SigPlaceholder,
            Body = UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(new UserCheckKnowYourCustomerCreateRequest
            {
                CheckConfirmedAt = _clock.GetUtcNow(),
                DataDownloadLink = "https://eshoponweb.example/kyc/" + userId.ToString("N"),
                DocumentType = DocumentType3.Passport,
                Provider = "eShopOnWeb",
                Method = UpvestInvestmentApi.Models.Enums.Method.VideoId,
            }),
        }, cancellationToken: ct), cancellationToken);

        // Tax residency — from the sign-up form's taxId/taxCountry. Response not consumed.
        await CallAsync("set tax residencies", isWrite: true, ct => _client.TaxResidencies.SetTaxResidencies(new SetTaxResidenciesRequest
        {
            UserId = userId,
            UpvestClientId = _clientId,
            Authorization = AuthPlaceholder,
            Signature = SigPlaceholder,
            SignatureInput = SigPlaceholder,
            IdempotencyKey = StableGuid(idempotencyScope, "tax"),
            Body = new TaxResidenciesSetRequest
            {
                TaxResidencies = new[]
                {
                    TaxResidencyForCreateRequest.WithTaxIdentifierNumber(new WithTaxIdentifierNumber
                    {
                        Country = taxCountry,
                        TaxIdentifierNumber = signUp.TaxId,
                    }),
                },
            },
        }, cancellationToken: ct), cancellationToken);

        return userId;
    }

    public async Task<OnboardingProgress> AdvanceOnboardingAsync(Guid userId, Guid? accountGroupId, Guid? accountId, string idempotencyScope, CancellationToken cancellationToken)
    {
        var userBody = await CallAsync("retrieve user", isWrite: false, ct => _client.Users.RetrieveUser(new RetrieveUserRequest
        {
            UserId = userId,
            UpvestClientId = _clientId,
            Authorization = AuthPlaceholder,
            Signature = SigPlaceholder,
            SignatureInput = SigPlaceholder,
        }, cancellationToken: ct), cancellationToken);

        var userStatus = GetString(userBody, "status");
        if (userStatus is "OFFBOARDING" or "OFFBOARDED")
            return new OnboardingProgress(accountGroupId, accountId, EnrolmentStatus.Rejected);
        if (userStatus != "ACTIVE")
            return new OnboardingProgress(accountGroupId, accountId, EnrolmentStatus.Pending);

        if (accountGroupId is null)
        {
            var groupBody = await CallAsync("create account group", isWrite: true, ct => _client.AccountGroups.CreateAccountGroup(new CreateAccountGroupRequest
            {
                UpvestClientId = _clientId,
                Authorization = AuthPlaceholder,
                Signature = SigPlaceholder,
                SignatureInput = SigPlaceholder,
                IdempotencyKey = StableGuid(idempotencyScope, "group"),
                Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
                {
                    UserId = userId,
                    Type = Type13.Personal,
                }),
            }, cancellationToken: ct), cancellationToken);
            accountGroupId = GetGuid(groupBody, "id");
        }

        if (accountId is null)
        {
            var accountBody = await CallAsync("create account", isWrite: true, ct => _client.AccountsApi.CreateAccount(new CreateAccountRequest
            {
                UpvestClientId = _clientId,
                Authorization = AuthPlaceholder,
                Signature = SigPlaceholder,
                SignatureInput = SigPlaceholder,
                IdempotencyKey = StableGuid(idempotencyScope, "account"),
                Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
                {
                    UserId = userId,
                    AccountGroupId = accountGroupId.Value,
                    Type = Type16.Trading,
                }),
            }, cancellationToken: ct), cancellationToken);
            accountId = GetGuid(accountBody, "id");
        }

        var accountReadBody = await CallAsync("retrieve account", isWrite: false, ct => _client.AccountsApi.RetrieveAccount(new RetrieveAccountRequest
        {
            AccountId = accountId.Value,
            UpvestClientId = _clientId,
            Authorization = AuthPlaceholder,
            Signature = SigPlaceholder,
            SignatureInput = SigPlaceholder,
        }, cancellationToken: ct), cancellationToken);

        var ready = GetString(accountReadBody, "status") == "ACTIVE";
        return new OnboardingProgress(accountGroupId, accountId, ready ? EnrolmentStatus.Active : EnrolmentStatus.Pending);
    }

    public async Task<UpvestInvestmentResult> PlaceInvestmentAsync(Guid userId, Guid accountGroupId, Guid accountId, decimal amountEuros, string clientReference, string idempotencyScope, CancellationToken cancellationToken)
    {
        var cashAmount = amountEuros.ToString("0.00", CultureInfo.InvariantCulture);

        // Move the shopper's set-aside change into their Upvest cash balance so the BUY can settle. This
        // credit and the order below share the same stable idempotency scope, so a retry does not double up.
        await CallAsync("fund cash balance", isWrite: true, ct => _client.VirtualCashBalances.CreateVirtualCashIncrease(new CreateVirtualCashIncreaseRequest
        {
            UpvestClientId = _clientId,
            Authorization = AuthPlaceholder,
            Signature = SigPlaceholder,
            SignatureInput = SigPlaceholder,
            IdempotencyKey = StableGuid(idempotencyScope, "funding"),
            Body = new VirtualCashBalanceVirtualCashIncreaseCreateRequest
            {
                AccountGroupId = accountGroupId,
                Amount = cashAmount,
                Currency = Currency1.Eur,
            },
        }, cancellationToken: ct), cancellationToken);

        var orderBody = await CallAsync("place order", isWrite: true, ct => _client.Orders.PlaceOrder(new PlaceOrderRequest
        {
            UpvestClientId = _clientId,
            Authorization = AuthPlaceholder,
            Signature = SigPlaceholder,
            SignatureInput = SigPlaceholder,
            IdempotencyKey = StableGuid(idempotencyScope, "order"),
            Body = new OrderPlaceRequest
            {
                UserId = userId,
                AccountId = accountId,
                Side = Side.Buy,
                InstrumentId = _settings.InstrumentId,
                CashAmount = cashAmount,
                Currency = Currency29.Eur,
                ClientReference = clientReference,
            },
        }, cancellationToken: ct), cancellationToken);

        return new UpvestInvestmentResult(GetGuid(orderBody, "id"), MapOrderStatus(GetString(orderBody, "status")));
    }

    public async Task<InvestmentStatus> GetInvestmentOutcomeAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var orderBody = await CallAsync("retrieve order", isWrite: false, ct => _client.Orders.RetrieveOrder(new RetrieveOrderRequest
        {
            OrderId = orderId,
            UpvestClientId = _clientId,
            Authorization = AuthPlaceholder,
            Signature = SigPlaceholder,
            SignatureInput = SigPlaceholder,
        }, cancellationToken: ct), cancellationToken);

        return MapOrderStatus(GetString(orderBody, "status"));
    }

    public async Task<UpvestInvestmentResult?> FindInvestmentByReferenceAsync(Guid accountId, string clientReference, CancellationToken cancellationToken)
    {
        var listBody = await CallAsync("list account orders", isWrite: false, ct => _client.Orders.ListAccountOrders(new ListAccountOrdersRequest
        {
            AccountId = accountId,
            UpvestClientId = _clientId,
            Authorization = AuthPlaceholder,
            Signature = SigPlaceholder,
            SignatureInput = SigPlaceholder,
        }, cancellationToken: ct), cancellationToken);

        using var doc = JsonDocument.Parse(listBody);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var order in data.EnumerateArray())
        {
            if (order.TryGetProperty("client_reference", out var cr)
                && string.Equals(cr.GetString(), clientReference, StringComparison.Ordinal))
            {
                var id = Guid.Parse(order.GetProperty("id").GetString()!);
                var status = MapOrderStatus(order.GetProperty("status").GetString()!);
                return new UpvestInvestmentResult(id, status);
            }
        }
        return null;
    }

    // ---- call + error boundary ------------------------------------------

    /// <summary>
    /// Issue an SDK call and return the raw response JSON. A 2xx whose body the SDK cannot deserialize is a
    /// success here (the provider omits members the model requires); a non-2xx is translated to a gateway error.
    /// </summary>
    private async Task<string> CallAsync(string operation, bool isWrite, Func<CancellationToken, Task> sdkCall, CancellationToken cancellationToken)
    {
        var holder = _capture.Begin();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);
        try
        {
            await sdkCall(cts.Token);
        }
        catch (ResponseDeserializationException ex) when ((int)ex.StatusCode is >= 200 and < 300)
        {
            // Success body the generated model could not bind — fall through and use the captured JSON.
        }
        catch (ResponseDeserializationException ex)
        {
            throw new UpvestGatewayException($"Upvest {operation} returned a response that could not be processed.",
                (int)ex.StatusCode, outcomeUnknown: false, ex);
        }
        catch (ApiException ex)
        {
            throw new UpvestGatewayException($"Upvest {operation} failed.", (int)ex.StatusCode, outcomeUnknown: false, ex);
        }
        catch (AuthSchemeException ex)
        {
            throw new UpvestGatewayException($"Upvest {operation} credentials could not be applied.", null, outcomeUnknown: false, ex);
        }
        catch (SdkTimeoutException ex)
        {
            throw new UpvestGatewayException($"Upvest {operation} timed out.", null, outcomeUnknown: isWrite, ex);
        }
        catch (SdkConnectionException ex)
        {
            throw new UpvestGatewayException($"Upvest {operation} could not be reached.", null, outcomeUnknown: isWrite, ex);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpvestGatewayException($"Upvest {operation} exceeded the time budget.", null, outcomeUnknown: isWrite);
        }

        if (string.IsNullOrEmpty(holder.Body))
            throw new UpvestGatewayException($"Upvest {operation} returned an empty response.");
        return holder.Body;
    }

    // ---- helpers ---------------------------------------------------------

    private static Guid GetGuid(string json, string field)
    {
        using var doc = JsonDocument.Parse(json);
        return Guid.Parse(doc.RootElement.GetProperty(field).GetString()!);
    }

    private static string GetString(string json, string field)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(field).GetString()!;
    }

    private static Guid StableGuid(string scope, string salt)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(scope + ":" + salt));
        return new Guid(hash);
    }

    private static Nationality ResolveNationality(string code) =>
        Nationality.TryGetKnownValue(code.ToUpperInvariant(), out var value)
            ? value
            : throw new UpvestGatewayException("Unsupported nationality code.", 400);

    private static Country ResolveCountry(string code) =>
        Country.TryGetKnownValue(code.ToUpperInvariant(), out var value)
            ? value
            : throw new UpvestGatewayException("Unsupported country code.", 400);

    private static InvestmentStatus MapOrderStatus(string status) => status switch
    {
        "FILLED" => InvestmentStatus.Settled,
        "CANCELLED" => InvestmentStatus.Failed,
        _ => InvestmentStatus.Pending
    };
}
