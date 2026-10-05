using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
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
/// The application's gateway onto Upvest. Every call is made through the Upvest SDK (and the one signing
/// handler). Response fields are read from the raw body captured by the handler, because several of the
/// service's 2xx bodies are leaner than the SDK's generated response models — invoking the SDK operation
/// still throws <see cref="ResponseDeserializationException"/>, which is tolerated here. Real API errors
/// (<see cref="ApiException"/>) and connection/timeout failures are translated to <see cref="UpvestApiException"/>.
/// </summary>
public sealed class UpvestGateway : IUpvestGateway
{
    // The header values here are placeholders; UpvestSigningHandler computes and overwrites the real
    // authorization/signature headers so no call site attaches credentials.
    private const string Ph = UpvestTokenProvider.SigningPlaceholder;

    private static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(30);

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestResponseCapture _capture;
    private readonly Guid _clientId;
    private readonly string _instrumentId;

    public UpvestGateway(UpvestInvestmentApiClient client, UpvestResponseCapture capture, IOptions<UpvestOptions> options)
    {
        _client = client;
        _capture = capture;
        _clientId = Guid.Parse(options.Value.ClientId);
        _instrumentId = options.Value.InstrumentId;
    }

    public async Task<string> CreateInvestorAsync(InvestorSignupForm form, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var nationality = KnownOrThrow(Nationality.TryGetKnownValue(form.Nationality, out var n), n, "nationality", form.Nationality);
        var country = KnownOrThrow(Country.TryGetKnownValue(form.Address.Country, out var c), c, "address country", form.Address.Country);

        var tol = new UserTolCreateRequest
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            Email = form.Email,
            BirthDate = new DateTimeOffset(form.BirthDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            Nationalities = new[] { nationality },
            Address = new Address { AddressLine1 = form.Address.Line1, Postcode = form.Address.Postcode, City = form.Address.City, Country = country },
            Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow },
            PhoneNumber = string.IsNullOrWhiteSpace(form.PhoneNumber) ? null : form.PhoneNumber,
        };

        var body = await CaptureAsync("create user", isWrite: true, ct => _client.Users.CreateUser(new CreateUserRequest
        {
            UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph, IdempotencyKey = idempotencyKey,
            Body = UserCreateRequest.UserTolCreateRequest(tol),
        }, cancellationToken: ct), cancellationToken);

        return Prop(body, "id");
    }

    public Task SubmitKycCheckAsync(string upvestUserId, CancellationToken cancellationToken)
    {
        var kyc = new UserCheckKnowYourCustomerCreateRequest
        {
            CheckConfirmedAt = DateTimeOffset.UtcNow,
            DataDownloadLink = "https://eshoponweb.example/kyc-evidence",
            DocumentType = DocumentType3.Passport,
            Provider = "eShopOnWeb",
            Method = Method.VideoId,
        };

        return CaptureAsync("create user check", isWrite: true, ct => _client.UserChecks.CreateUserCheck(new CreateUserCheckRequest
        {
            UserId = Guid.Parse(upvestUserId), UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph,
            Body = UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(kyc),
        }, cancellationToken: ct), cancellationToken);
    }

    public Task SetTaxResidencyAsync(string upvestUserId, string taxCountry, string taxId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var country = KnownOrThrow(Country.TryGetKnownValue(taxCountry, out var c), c, "tax country", taxCountry);

        var tax = new TaxResidenciesSetRequest
        {
            TaxResidencies = new[]
            {
                (TaxResidencyForCreateRequest)new WithTaxIdentifierNumber { Country = country, TaxIdentifierNumber = taxId },
            },
        };

        return CaptureAsync("set tax residencies", isWrite: true, ct => _client.TaxResidencies.SetTaxResidencies(new SetTaxResidenciesRequest
        {
            UserId = Guid.Parse(upvestUserId), UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph, IdempotencyKey = idempotencyKey,
            Body = tax,
        }, cancellationToken: ct), cancellationToken);
    }

    public async Task<string> GetUserStatusAsync(string upvestUserId, CancellationToken cancellationToken)
    {
        var body = await CaptureAsync("retrieve user", isWrite: false, ct => _client.Users.RetrieveUser(new RetrieveUserRequest
        {
            UserId = Guid.Parse(upvestUserId), UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph,
        }, cancellationToken: ct), cancellationToken);
        return Prop(body, "status");
    }

    public async Task<string> CreateAccountGroupAsync(string upvestUserId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = await CaptureAsync("create account group", isWrite: true, ct => _client.AccountGroups.CreateAccountGroup(new CreateAccountGroupRequest
        {
            UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph, IdempotencyKey = idempotencyKey,
            Body = (AccountGroupCreateRequest)new AccountGroupCreateUserRequest { UserId = Guid.Parse(upvestUserId), Type = Type13.Personal },
        }, cancellationToken: ct), cancellationToken);
        return Prop(body, "id");
    }

    public async Task<string> CreateAccountAsync(string upvestUserId, string accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = await CaptureAsync("create account", isWrite: true, ct => _client.AccountsApi.CreateAccount(new CreateAccountRequest
        {
            UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph, IdempotencyKey = idempotencyKey,
            Body = (AccountCreateRequest)new AccountCreateUserRequest
            {
                UserId = Guid.Parse(upvestUserId), AccountGroupId = Guid.Parse(accountGroupId), Type = Type16.Trading, Name = "eShop change investing",
            },
        }, cancellationToken: ct), cancellationToken);
        return Prop(body, "id");
    }

    public async Task<string> GetAccountStatusAsync(string accountId, CancellationToken cancellationToken)
    {
        var body = await CaptureAsync("retrieve account", isWrite: false, ct => _client.AccountsApi.RetrieveAccount(new RetrieveAccountRequest
        {
            AccountId = Guid.Parse(accountId), UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph,
        }, cancellationToken: ct), cancellationToken);
        return Prop(body, "status");
    }

    public Task FundAccountGroupAsync(string accountGroupId, long amountCents, Guid idempotencyKey, CancellationToken cancellationToken) =>
        CaptureAsync("create top-up", isWrite: true, ct => _client.TopUps.CreateTopup(new CreateTopupRequest
        {
            UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph, IdempotencyKey = idempotencyKey,
            Body = new PaymentsTopUpCreateRequest { AccountGroupId = Guid.Parse(accountGroupId), CashAmount = Money(amountCents), Currency = Currency.Eur },
        }, cancellationToken: ct), cancellationToken);

    public async Task<string> PlaceBuyOrderAsync(string upvestUserId, string accountId, long amountCents, string clientReference, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = await CaptureAsync("place order", isWrite: true, ct => _client.Orders.PlaceOrder(new PlaceOrderRequest
        {
            UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph, IdempotencyKey = idempotencyKey,
            Body = new OrderPlaceRequest
            {
                AccountId = Guid.Parse(accountId), UserId = Guid.Parse(upvestUserId), Side = Side.Buy,
                InstrumentId = _instrumentId, CashAmount = Money(amountCents), Currency = Currency29.Eur,
                OrderType = OrderType.Market, ClientReference = clientReference,
            },
        }, cancellationToken: ct), cancellationToken);
        return Prop(body, "id");
    }

    public async Task<string> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken)
    {
        var body = await CaptureAsync("retrieve order", isWrite: false, ct => _client.Orders.RetrieveOrder(new RetrieveOrderRequest
        {
            OrderId = Guid.Parse(orderId), UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph,
        }, cancellationToken: ct), cancellationToken);
        return Prop(body, "status");
    }

    public async Task<UpvestOrderInfo?> FindOrderByReferenceAsync(string accountId, string clientReference, CancellationToken cancellationToken)
    {
        var body = await CaptureAsync("list account orders", isWrite: false, ct => _client.Orders.ListAccountOrders(new ListAccountOrdersRequest
        {
            AccountId = Guid.Parse(accountId), UpvestClientId = _clientId, Authorization = Ph, Signature = Ph, SignatureInput = Ph, Limit = 1000,
        }, cancellationToken: ct), cancellationToken);

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var order in data.EnumerateArray())
        {
            if (order.TryGetProperty("client_reference", out var r) && r.GetString() == clientReference)
                return new UpvestOrderInfo(order.GetProperty("id").GetString()!, order.GetProperty("status").GetString()!);
        }

        return null;
    }

    // --- helpers ------------------------------------------------------------------------------------

    /// <summary>
    /// Runs an SDK call bounded by a total-call deadline, capturing the raw response. Tolerates the
    /// deserialization skew on success bodies; translates real API errors and connection failures.
    /// </summary>
    private async Task<string> CaptureAsync(string operation, bool isWrite, Func<CancellationToken, Task> call, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallBudget);

        var slot = _capture.Begin();
        try
        {
            await call(cts.Token);
        }
        catch (ResponseDeserializationException)
        {
            // Success body leaner than the generated model — the body is captured; fall through.
        }
        catch (ApiException ex)
        {
            throw new UpvestApiException($"Upvest '{operation}' was rejected.", (int)ex.StatusCode, innerException: ex);
        }
        catch (SdkException ex)
        {
            // No usable response: on a write the outcome is unknown and must be reconciled.
            throw new UpvestApiException($"Upvest '{operation}' did not complete.", outcomeUnknown: isWrite, innerException: ex);
        }
        finally
        {
            _capture.End();
        }

        if (slot.Body is null)
            throw new UpvestApiException($"Upvest '{operation}' returned no response body.");

        if (slot.StatusCode is < 200 or >= 300)
            throw new UpvestApiException($"Upvest '{operation}' was rejected.", slot.StatusCode);

        return slot.Body;
    }

    private static string Prop(string rawJson, string name)
    {
        using var doc = JsonDocument.Parse(rawJson);
        if (doc.RootElement.TryGetProperty(name, out var value) && value.GetString() is { } s)
            return s;
        throw new UpvestApiException($"Upvest response did not contain '{name}'.");
    }

    private static T KnownOrThrow<T>(bool found, T? value, string field, string raw) where T : class =>
        found && value is not null ? value : throw new UpvestApiException($"Unsupported {field} code '{raw}'.");

    /// <summary>Formats euro cents as the "d.dd" string Upvest expects.</summary>
    private static string Money(long cents) =>
        (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
