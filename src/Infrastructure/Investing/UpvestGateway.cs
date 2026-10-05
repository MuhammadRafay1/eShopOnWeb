using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;
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

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Talks to Upvest through the generated SDK client. Requests are built with the SDK's typed models;
/// responses are read from the raw body captured by <see cref="UpvestResponseCapture"/>, because many of
/// Upvest's 2xx bodies do not satisfy the SDK models' strict <c>required</c> fields and would otherwise
/// surface as <c>ResponseDeserializationException</c>. Success/failure is decided by the captured HTTP status.
/// </summary>
public sealed class UpvestGateway : IUpvestGateway
{
    // The handler overwrites these; they exist only to satisfy the SDK's required request members.
    private const string PlaceholderBearer = "Bearer placeholder";

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestResponseCapture _capture;
    private readonly UpvestOptions _options;
    private readonly ILogger<UpvestGateway> _logger;
    private readonly Guid _clientGuid;

    public UpvestGateway(
        UpvestInvestmentApiClient client,
        UpvestResponseCapture capture,
        UpvestOptions options,
        ILogger<UpvestGateway> logger)
    {
        _client = client;
        _capture = capture;
        _options = options;
        _logger = logger;
        _clientGuid = Guid.Parse(options.ClientId);
    }

    public async Task<UpvestEnrolmentResult> EnrolAsync(InvestorEnrolmentForm form, CancellationToken cancellationToken)
    {
        var userBody = new UserTolCreateRequest
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            Email = form.Email,
            BirthDate = ParseDate(form.BirthDate),
            Nationalities = new[] { ParseNationality(form.Nationality) },
            Address = new Address
            {
                AddressLine1 = form.AddressLine1,
                Postcode = form.Postcode,
                City = form.City,
                Country = ParseCountry(form.Country)
            },
            Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow },
            PhoneNumber = string.IsNullOrWhiteSpace(form.PhoneNumber) ? null : form.PhoneNumber
        };

        var createUser = new CreateUserRequest
        {
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = Guid.NewGuid(),
            Body = UserCreateRequest.UserTolCreateRequest(userBody)
        };

        var userJson = await InvokeAsync("create_user",
            ct => _client.Users.CreateUser(createUser, cancellationToken: ct), cancellationToken);
        var userId = GetString(userJson, "id");
        var status = GetString(userJson, "status");

        var checkBody = new UserCheckKnowYourCustomerCreateRequest
        {
            CheckConfirmedAt = DateTimeOffset.UtcNow,
            DataDownloadLink = $"https://eshoponweb.example/kyc/{userId}",
            DocumentType = DocumentType3.Passport,
            Provider = "eshoponweb-sandbox",
            Method = Method.VideoId
        };
        var createCheck = new CreateUserCheckRequest
        {
            UserId = Guid.Parse(userId),
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            Body = UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(checkBody)
        };
        await InvokeAsync("create_user_check",
            ct => _client.UserChecks.CreateUserCheck(createCheck, cancellationToken: ct), cancellationToken);

        var taxBody = new TaxResidenciesSetRequest
        {
            TaxResidencies = new[]
            {
                TaxResidencyForCreateRequest.WithTaxIdentifierNumber(new WithTaxIdentifierNumber
                {
                    Country = ParseCountry(form.TaxCountry),
                    TaxIdentifierNumber = form.TaxId
                })
            }
        };
        var setTax = new SetTaxResidenciesRequest
        {
            UserId = Guid.Parse(userId),
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = Guid.NewGuid(),
            Body = taxBody
        };
        await InvokeAsync("set_tax_residencies",
            ct => _client.TaxResidencies.SetTaxResidencies(setTax, cancellationToken: ct), cancellationToken);

        return new UpvestEnrolmentResult(userId, status);
    }

    public async Task<string> GetUserStatusAsync(string upvestUserId, CancellationToken cancellationToken)
    {
        var req = new RetrieveUserRequest
        {
            UserId = Guid.Parse(upvestUserId),
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty
        };
        var json = await InvokeAsync("retrieve_user",
            ct => _client.Users.RetrieveUser(req, cancellationToken: ct), cancellationToken);
        return GetString(json, "status");
    }

    public async Task<UpvestAccountSetupResult> CreateAccountSetupAsync(
        string upvestUserId, Guid groupIdempotencyKey, Guid accountIdempotencyKey, CancellationToken cancellationToken)
    {
        var groupReq = new CreateAccountGroupRequest
        {
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = groupIdempotencyKey,
            Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
            {
                UserId = Guid.Parse(upvestUserId),
                Type = Type13.Personal
            })
        };
        var groupJson = await InvokeAsync("create_account_group",
            ct => _client.AccountGroups.CreateAccountGroup(groupReq, cancellationToken: ct), cancellationToken);
        var accountGroupId = GetString(groupJson, "id");

        var accountReq = new CreateAccountRequest
        {
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = accountIdempotencyKey,
            Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
            {
                UserId = Guid.Parse(upvestUserId),
                AccountGroupId = Guid.Parse(accountGroupId),
                Type = Type16.Trading
            })
        };
        var accountJson = await InvokeAsync("create_account",
            ct => _client.AccountsApi.CreateAccount(accountReq, cancellationToken: ct), cancellationToken);

        return new UpvestAccountSetupResult(accountGroupId, GetString(accountJson, "id"), GetString(accountJson, "status"));
    }

    public async Task<string> GetAccountStatusAsync(string accountId, CancellationToken cancellationToken)
    {
        var req = new RetrieveAccountRequest
        {
            AccountId = Guid.Parse(accountId),
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty
        };
        var json = await InvokeAsync("retrieve_account",
            ct => _client.AccountsApi.RetrieveAccount(req, cancellationToken: ct), cancellationToken);
        return GetString(json, "status");
    }

    public async Task FundAsync(string accountGroupId, decimal amountEuros, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var req = new CreateTopupRequest
        {
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = idempotencyKey,
            Body = new PaymentsTopUpCreateRequest
            {
                AccountGroupId = Guid.Parse(accountGroupId),
                CashAmount = Amount(amountEuros),
                Currency = Currency.Eur
            }
        };
        await InvokeAsync("create_topup",
            ct => _client.TopUps.CreateTopup(req, cancellationToken: ct), cancellationToken);
    }

    public async Task<string> PlaceInvestmentOrderAsync(
        string upvestUserId, string accountId, decimal amountEuros, string clientReference, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var req = new PlaceOrderRequest
        {
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = idempotencyKey,
            Body = new OrderPlaceRequest
            {
                UserId = Guid.Parse(upvestUserId),
                AccountId = Guid.Parse(accountId),
                Side = Side.Buy,
                InstrumentId = _options.InstrumentId,
                CashAmount = Amount(amountEuros),
                Currency = Currency29.Eur,
                OrderType = OrderType.Market,
                UserInstrumentFitAcknowledgement = true,
                ClientReference = clientReference
            }
        };
        var json = await InvokeAsync("place_order",
            ct => _client.Orders.PlaceOrder(req, cancellationToken: ct), cancellationToken);
        return GetString(json, "id");
    }

    public async Task<UpvestOrderState> GetOrderAsync(string upvestOrderId, CancellationToken cancellationToken)
    {
        var req = new RetrieveOrderRequest
        {
            OrderId = Guid.Parse(upvestOrderId),
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty
        };
        var json = await InvokeAsync("retrieve_order",
            ct => _client.Orders.RetrieveOrder(req, cancellationToken: ct), cancellationToken);
        return new UpvestOrderState(GetString(json, "id"), GetString(json, "status"));
    }

    public async Task<UpvestOrderState?> FindOrderByReferenceAsync(string accountId, string clientReference, CancellationToken cancellationToken)
    {
        var req = new ListAccountOrdersRequest
        {
            AccountId = Guid.Parse(accountId),
            UpvestClientId = _clientGuid,
            Authorization = PlaceholderBearer,
            Signature = string.Empty,
            SignatureInput = string.Empty
        };
        var json = await InvokeAsync("list_account_orders",
            ct => _client.Orders.ListAccountOrders(req, cancellationToken: ct), cancellationToken);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var order in data.EnumerateArray())
            {
                if (order.TryGetProperty("client_reference", out var cr) &&
                    string.Equals(cr.GetString(), clientReference, StringComparison.Ordinal))
                {
                    return new UpvestOrderState(order.GetProperty("id").GetString()!, order.GetProperty("status").GetString()!);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Runs one SDK operation and returns the raw 2xx response body. Errors (incl. deserialization failures)
    /// are decided by the captured HTTP status, not by exception type.
    /// </summary>
    private async Task<string> InvokeAsync(string operation, Func<CancellationToken, Task> sdkCall, CancellationToken cancellationToken)
    {
        var box = _capture.Begin();
        try
        {
            await sdkCall(cancellationToken);
        }
        catch (SdkConnectionException ex)
        {
            // Covers SdkTimeoutException too: no usable response — the outcome may be unknown for writes.
            throw new UpvestUnavailableException(operation, ex);
        }
        catch (AuthSchemeException ex)
        {
            throw new UpvestUnavailableException(operation, ex);
        }
        catch (ApiException)
        {
            // The server answered (an error status, or a 2xx body the SDK could not deserialize).
            // Fall through to the captured status/body.
        }

        if (!box.Recorded)
        {
            throw new UpvestUnavailableException(operation);
        }
        if (box.StatusCode >= 400)
        {
            throw new UpvestApiException(operation, box.StatusCode, ExtractDetail(box.Body));
        }
        return box.Body;
    }

    private static string Amount(decimal euros) => euros.ToString("0.00", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDate(string isoDate) =>
        DateTimeOffset.Parse(isoDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private Nationality ParseNationality(string code)
    {
        if (Nationality.TryGetKnownValue(code.Trim().ToUpperInvariant(), out var value))
        {
            return value;
        }
        throw new UpvestApiException("create_user", 400, $"Unsupported nationality '{code}'.");
    }

    private Country ParseCountry(string code)
    {
        if (Country.TryGetKnownValue(code.Trim().ToUpperInvariant(), out var value))
        {
            return value;
        }
        throw new UpvestApiException("create_user", 400, $"Unsupported country '{code}'.");
    }

    private static string GetString(string json, string property)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(property, out var value))
        {
            return value.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static string? ExtractDetail(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
            // non-JSON error body
        }
        return null;
    }
}
