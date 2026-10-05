using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.AccountGroups;
using UpvestInvestmentApi.Requests.AccountsApi;
using UpvestInvestmentApi.Requests.CashBalances;
using UpvestInvestmentApi.Requests.Orders;
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.TopUps;
using UpvestInvestmentApi.Requests.UserChecks;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.WebhookSubscriptions;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Talks to Upvest through the generated SDK (the only place that does). Request bodies are built with SDK
/// models; response fields (id/status) are read from the raw body captured by the authentication handler,
/// because several Upvest 2xx responses omit members the SDK models mark <c>required</c> and would otherwise
/// fail typed deserialization. Credentials are applied solely by the handler, never here.
/// </summary>
public sealed class UpvestGateway : IUpvestGateway
{
    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestResponseCapture _capture;
    private readonly UpvestOptions _options;

    public UpvestGateway(UpvestInvestmentApiClient client, UpvestResponseCapture capture, UpvestOptions options)
    {
        _client = client;
        _capture = capture;
        _options = options;
    }

    public string InstrumentId => _options.InstrumentId;

    // Placeholder values for the credential members: the handler overwrites upvest-client-id and sets the
    // authorization/signature headers, so no real credential is attached at the call site.
    private static Guid NoClientId => Guid.Empty;
    private static readonly string NoHeader = null!;

    public async Task<Guid> CreateInvestorAsync(UpvestInvestorDetails d, Guid idempotencyKey, CancellationToken ct)
    {
        var body = new UserTolCreateRequest
        {
            FirstName = d.FirstName,
            LastName = d.LastName,
            Email = d.Email,
            BirthDate = d.BirthDate,
            Nationalities = new[] { OpenEnum<Nationality>(d.Nationality) },
            PhoneNumber = d.PhoneNumber,
            Address = new Address
            {
                AddressLine1 = d.AddressLine1,
                Postcode = d.Postcode,
                City = d.City,
                Country = OpenEnum<Country>(d.Country),
            },
            Fatca = new Fatca { Status = false, ConfirmedAt = d.ConsentTimestamp },
        };

        var json = await InvokeAsync("create-user", ct2 => _client.Users.CreateUser(new CreateUserRequest
        {
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
            IdempotencyKey = idempotencyKey,
            UpvestApiVersion = UpvestApiVersion._1,
            Body = body,
        }, cancellationToken: ct2), ct);

        return GetGuid(json, "id");
    }

    public async Task SubmitKycCheckAsync(Guid userId, CancellationToken ct)
    {
        var body = new UserCheckKnowYourCustomerCreateRequest
        {
            CheckConfirmedAt = DateTimeOffset.UtcNow,
            DataDownloadLink = $"{_options.CallbackBaseUrl.TrimEnd('/')}/kyc/{userId}",
            DocumentType = DocumentType3.Passport,
            Provider = "eShopOnWeb",
            Method = Method.VideoId,
        };

        await InvokeAsync("create-user-check", ct2 => _client.UserChecks.CreateUserCheck(new CreateUserCheckRequest
        {
            UserId = userId,
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
            UpvestApiVersion = UpvestApiVersion._1,
            Body = body,
        }, cancellationToken: ct2), ct);
    }

    public async Task SetTaxResidencyAsync(Guid userId, string taxCountry, string taxId, Guid idempotencyKey, CancellationToken ct)
    {
        var body = new TaxResidenciesSetRequest
        {
            TaxResidencies = new TaxResidencyForCreateRequest[]
            {
                new WithTaxIdentifierNumber
                {
                    Country = OpenEnum<Country>(taxCountry),
                    TaxIdentifierNumber = taxId,
                },
            },
        };

        await InvokeAsync("set-tax-residencies", ct2 => _client.TaxResidencies.SetTaxResidencies(new SetTaxResidenciesRequest
        {
            UserId = userId,
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
            IdempotencyKey = idempotencyKey,
            UpvestApiVersion = UpvestApiVersion._1,
            Body = body,
        }, cancellationToken: ct2), ct);
    }

    public async Task<UpvestUserState> GetUserStateAsync(Guid userId, CancellationToken ct)
    {
        var json = await InvokeAsync("retrieve-user", ct2 => _client.Users.RetrieveUser(new RetrieveUserRequest
        {
            UserId = userId,
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
        }, cancellationToken: ct2), ct);

        return GetString(json, "status") switch
        {
            "ACTIVE" => UpvestUserState.Active,
            "INACTIVE" or "PENDING" or "CREATED" or "PROCESSING" => UpvestUserState.Pending,
            _ => UpvestUserState.Rejected,
        };
    }

    public async Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken ct)
    {
        var body = new AccountGroupCreateUserRequest { UserId = userId, Type = Type13.Personal };

        var json = await InvokeAsync("create-account-group", ct2 => _client.AccountGroups.CreateAccountGroup(new CreateAccountGroupRequest
        {
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
            IdempotencyKey = idempotencyKey,
            UpvestApiVersion = UpvestApiVersion._1,
            Body = body,
        }, cancellationToken: ct2), ct);

        return GetGuid(json, "id");
    }

    public async Task<Guid> CreateTradingAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken ct)
    {
        var body = new AccountCreateUserRequest
        {
            UserId = userId,
            AccountGroupId = accountGroupId,
            Type = Type16.Trading,
            Name = "Invest your change",
        };

        var json = await InvokeAsync("create-account", ct2 => _client.AccountsApi.CreateAccount(new CreateAccountRequest
        {
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
            IdempotencyKey = idempotencyKey,
            UpvestApiVersion = UpvestApiVersion._1,
            Body = body,
        }, cancellationToken: ct2), ct);

        return GetGuid(json, "id");
    }

    public async Task<UpvestAccountState> GetAccountStateAsync(Guid accountId, CancellationToken ct)
    {
        var json = await InvokeAsync("retrieve-account", ct2 => _client.AccountsApi.RetrieveAccount(new RetrieveAccountRequest
        {
            AccountId = accountId,
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
        }, cancellationToken: ct2), ct);

        return GetString(json, "status") switch
        {
            "ACTIVE" => UpvestAccountState.Active,
            "PENDING_APPROVAL" or "PENDING" or "PROCESSING" => UpvestAccountState.Pending,
            _ => UpvestAccountState.Rejected,
        };
    }

    public async Task<Guid> CreateTopupAsync(Guid accountGroupId, long amountCents, Guid idempotencyKey, CancellationToken ct)
    {
        var body = new PaymentsTopUpCreateRequest
        {
            AccountGroupId = accountGroupId,
            CashAmount = Money(amountCents),
            Currency = Currency.Eur,
        };

        var json = await InvokeAsync("create-topup", ct2 => _client.TopUps.CreateTopup(new CreateTopupRequest
        {
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
            IdempotencyKey = idempotencyKey,
            UpvestApiVersion = UpvestApiVersion._1,
            Body = body,
        }, cancellationToken: ct2), ct);

        return GetGuid(json, "id");
    }

    public async Task<long> GetAvailableCashCentsAsync(Guid accountGroupId, CancellationToken ct)
    {
        var json = await InvokeAsync("retrieve-cash-balance", ct2 => _client.CashBalances.RetrieveCashBalance(new RetrieveCashBalanceRequest
        {
            AccountGroupId = accountGroupId,
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
        }, cancellationToken: ct2), ct);

        var available = GetString(json, "available");
        return decimal.TryParse(available, NumberStyles.Number, CultureInfo.InvariantCulture, out var euros)
            ? (long)Math.Round(euros * 100m, MidpointRounding.AwayFromZero)
            : 0;
    }

    public async Task<Guid> PlaceBuyOrderAsync(Guid accountId, long amountCents, string clientReference, Guid idempotencyKey, CancellationToken ct)
    {
        var body = new OrderPlaceRequest
        {
            AccountId = accountId,
            Side = Side.Buy,
            InstrumentId = _options.InstrumentId,
            CashAmount = Money(amountCents),
            Currency = Currency29.Eur,
            OrderType = OrderType.Market,
            ClientReference = clientReference,
        };

        var json = await InvokeAsync("place-order", ct2 => _client.Orders.PlaceOrder(new PlaceOrderRequest
        {
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
            IdempotencyKey = idempotencyKey,
            UpvestApiVersion = UpvestApiVersion._1,
            Body = body,
        }, cancellationToken: ct2), ct);

        return GetGuid(json, "id");
    }

    public async Task<UpvestOrderState> GetOrderStateAsync(Guid orderId, CancellationToken ct)
    {
        var json = await InvokeAsync("retrieve-order", ct2 => _client.Orders.RetrieveOrder(new RetrieveOrderRequest
        {
            OrderId = orderId,
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
        }, cancellationToken: ct2), ct);

        return GetString(json, "status") switch
        {
            "FILLED" or "SETTLED" or "COMPLETED" => UpvestOrderState.Filled,
            "CANCELLED" or "REJECTED" or "EXPIRED" or "FAILED" => UpvestOrderState.Cancelled,
            _ => UpvestOrderState.Pending,
        };
    }

    public async Task EnsureWebhookAsync(string callbackUrl, CancellationToken ct)
    {
        var listJson = await InvokeAsync("list-webhooks", ct2 => _client.WebhookSubscriptions.ListWebhooks(new ListWebhooksRequest
        {
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
        }, cancellationToken: ct2), ct);

        using (var doc = JsonDocument.Parse(listJson))
        {
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var w in data.EnumerateArray())
                {
                    if (w.TryGetProperty("url", out var u) && string.Equals(u.GetString(), callbackUrl, StringComparison.OrdinalIgnoreCase))
                        return; // already subscribed
                }
            }
        }

        var body = new WebhookCreateRequest { Title = "eShopOnWeb invest-your-change", Url = callbackUrl };
        await InvokeAsync("create-webhook", ct2 => _client.WebhookSubscriptions.CreateWebhook(new CreateWebhookRequest
        {
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
            UpvestApiVersion = UpvestApiVersion._1,
            Body = body,
        }, cancellationToken: ct2), ct);
    }

    public async Task<UpvestWebhookKey[]> GetWebhookKeysAsync(CancellationToken ct)
    {
        var json = await InvokeAsync("get-jwks", ct2 => _client.WebhookSubscriptions.GetJwks(new GetJwksRequest
        {
            UpvestClientId = NoClientId,
            Authorization = NoHeader,
            Signature = NoHeader,
            SignatureInput = NoHeader,
        }, cancellationToken: ct2), ct);

        using var doc = JsonDocument.Parse(json);
        var keys = new List<UpvestWebhookKey>();
        if (doc.RootElement.TryGetProperty("keys", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var k in arr.EnumerateArray())
            {
                keys.Add(new UpvestWebhookKey(
                    k.TryGetProperty("kid", out var kid) ? kid.GetString() ?? "" : "",
                    k.TryGetProperty("crv", out var crv) ? crv.GetString() ?? "" : "",
                    k.TryGetProperty("x", out var x) ? x.GetString() ?? "" : "",
                    k.TryGetProperty("y", out var y) ? y.GetString() ?? "" : ""));
            }
        }
        return keys.ToArray();
    }

    // ---- plumbing ---------------------------------------------------------------------------------------

    private async Task<string> InvokeAsync(string op, Func<CancellationToken, Task> call, CancellationToken ct)
    {
        var slot = _capture.Begin();
        try
        {
            await call(ct).ConfigureAwait(false);
        }
        catch (ResponseDeserializationException)
        {
            // A 2xx body that doesn't fit the SDK model is success here — read the captured body below.
            // A non-2xx one falls through to the status check and is reported as a failure.
        }
        catch (ApiException ex)
        {
            var code = (int)ex.StatusCode;
            throw new UpvestGatewayException($"Upvest {op} failed with status {code}.", IsTransient(code), code, ex);
        }
        catch (SdkTimeoutException ex)
        {
            throw new UpvestGatewayException($"Upvest {op} timed out.", true, inner: ex);
        }
        catch (SdkConnectionException ex)
        {
            throw new UpvestGatewayException($"Upvest {op} could not reach the provider.", true, inner: ex);
        }
        catch (AuthSchemeException ex)
        {
            throw new UpvestGatewayException($"Upvest {op} credentials could not be applied.", false, inner: ex);
        }

        if (slot.Status is not { } status)
            throw new UpvestGatewayException($"Upvest {op} produced no response.", true);

        var statusCode = (int)status;
        if (statusCode >= 400)
            throw new UpvestGatewayException($"Upvest {op} failed with status {statusCode}.", IsTransient(statusCode), statusCode);

        return slot.Body;
    }

    private static bool IsTransient(int status) => status is 408 or 429 || status >= 500;

    private static string Money(long cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private static T OpenEnum<T>(string wireValue) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(wireValue))!;

    private static Guid GetGuid(string json, string property)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty(property, out var v) && Guid.TryParse(v.GetString(), out var g)
            ? g
            : throw new UpvestGatewayException($"Upvest response did not contain a '{property}' id.", false);
    }

    private static string GetString(string json, string property)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty(property, out var v) ? v.GetString() ?? "" : "";
    }
}
