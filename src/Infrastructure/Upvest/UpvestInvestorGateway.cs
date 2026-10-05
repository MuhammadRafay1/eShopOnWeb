using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.Extensions.Options;
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
/// The provider gateway, implemented over the Upvest SDK. Every call goes through the SDK client (and so
/// through the single signing/auth handler). Because this sandbox returns success bodies that omit some
/// members the SDK's generated models mark <c>required</c>, each call captures the raw response via an
/// <see cref="SdkHook"/> and reads the fields it needs from it — tolerating the
/// <see cref="ResponseDeserializationException"/> the strict model would otherwise raise on a 2xx. All
/// provider/transport failures are translated to <see cref="UpvestGatewayException"/>.
/// </summary>
public sealed class UpvestInvestorGateway : IUpvestInvestorGateway
{
    private const string AuthorizationPlaceholder = "Bearer placeholder"; // the handler overwrites this
    private const string KycEvidenceBaseUrl = "https://kyc-evidence.eshoponweb.example/evidence";

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestOptions _options;

    public UpvestInvestorGateway(UpvestInvestmentApiClient client, IOptions<UpvestOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<InvestorCreated> CreateInvestorAsync(InvestorSignUpDetails details, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var body = BuildUserBody(details);
        var root = await CallAsync(opts => _client.Users.CreateUser(new CreateUserRequest
        {
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = idempotencyKey,
            Body = body,
        }, opts, cancellationToken), isWrite: true, "CreateUser", cancellationToken);

        var userId = GetGuid(root, "id");
        var status = MapInvestorStatus(GetString(root, "status"));
        return new InvestorCreated(userId, status);
    }

    public async Task SubmitOnboardingEvidenceAsync(Guid upvestUserId, InvestorSignUpDetails details, Guid taxIdempotencyKey, CancellationToken cancellationToken)
    {
        // KYC check (the generator injects the idempotency key for this operation).
        await CallAsync(opts => _client.UserChecks.CreateUserCheck(new CreateUserCheckRequest
        {
            UserId = upvestUserId,
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            Body = new UserCheckKnowYourCustomerCreateRequest
            {
                CheckConfirmedAt = DateTimeOffset.UtcNow,
                DataDownloadLink = $"{KycEvidenceBaseUrl}/{upvestUserId}",
                DocumentType = DocumentType3.IdCard,
                Provider = "eShopOnWeb",
                Method = Method.VideoId,
            },
        }, opts, cancellationToken), isWrite: true, "CreateUserCheck", cancellationToken);

        // Tax residency.
        await CallAsync(opts => _client.TaxResidencies.SetTaxResidencies(new SetTaxResidenciesRequest
        {
            UserId = upvestUserId,
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = taxIdempotencyKey,
            Body = new TaxResidenciesSetRequest
            {
                TaxResidencies = new TaxResidencyForCreateRequest[]
                {
                    new WithTaxIdentifierNumber
                    {
                        Country = ResolveCountry(details.TaxCountry, "TaxCountry"),
                        TaxIdentifierNumber = details.TaxId,
                    },
                },
            },
        }, opts, cancellationToken), isWrite: true, "SetTaxResidencies", cancellationToken);
    }

    public async Task<ProviderInvestorStatus> GetInvestorStatusAsync(Guid upvestUserId, CancellationToken cancellationToken)
    {
        var root = await CallAsync(opts => _client.Users.RetrieveUser(new RetrieveUserRequest
        {
            UserId = upvestUserId,
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
        }, opts, cancellationToken), isWrite: false, "RetrieveUser", cancellationToken);

        return MapInvestorStatus(GetString(root, "status"));
    }

    public async Task<Guid> CreateAccountGroupAsync(Guid upvestUserId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var root = await CallAsync(opts => _client.AccountGroups.CreateAccountGroup(new CreateAccountGroupRequest
        {
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = idempotencyKey,
            Body = new AccountGroupCreateUserRequest { UserId = upvestUserId, Type = Type13.Personal },
        }, opts, cancellationToken), isWrite: true, "CreateAccountGroup", cancellationToken);

        return GetGuid(root, "id");
    }

    public async Task<Guid> CreateAccountAsync(Guid upvestUserId, Guid accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var root = await CallAsync(opts => _client.AccountsApi.CreateAccount(new CreateAccountRequest
        {
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = idempotencyKey,
            Body = new AccountCreateUserRequest { UserId = upvestUserId, AccountGroupId = accountGroupId, Type = Type16.Trading },
        }, opts, cancellationToken), isWrite: true, "CreateAccount", cancellationToken);

        return GetGuid(root, "id");
    }

    public async Task<bool> IsAccountActiveAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var root = await CallAsync(opts => _client.AccountsApi.RetrieveAccount(new RetrieveAccountRequest
        {
            AccountId = accountId,
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
        }, opts, cancellationToken), isWrite: false, "RetrieveAccount", cancellationToken);

        return string.Equals(GetString(root, "status"), "ACTIVE", StringComparison.OrdinalIgnoreCase);
    }

    public async Task TopUpAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        await CallAsync(opts => _client.TopUps.CreateTopup(new CreateTopupRequest
        {
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = idempotencyKey,
            Body = new PaymentsTopUpCreateRequest
            {
                AccountGroupId = accountGroupId,
                CashAmount = FormatMoney(amount),
                Currency = Currency.Eur,
            },
        }, opts, cancellationToken), isWrite: true, "CreateTopup", cancellationToken);
    }

    public async Task<Guid> PlaceInvestmentOrderAsync(Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        var root = await CallAsync(opts => _client.Orders.PlaceOrder(new PlaceOrderRequest
        {
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = idempotencyKey,
            Body = new OrderPlaceRequest
            {
                AccountId = accountId,
                Side = Side.Buy,
                InstrumentId = _options.InstrumentId,
                CashAmount = FormatMoney(amount),
                Currency = Currency29.Eur,
            },
        }, opts, cancellationToken), isWrite: true, "PlaceOrder", cancellationToken);

        return GetGuid(root, "id");
    }

    public async Task<ProviderOrderOutcome> GetOrderOutcomeAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var root = await CallAsync(opts => _client.Orders.RetrieveOrder(new RetrieveOrderRequest
        {
            OrderId = orderId,
            UpvestClientId = _options.ClientIdGuid,
            Authorization = AuthorizationPlaceholder,
            Signature = string.Empty,
            SignatureInput = string.Empty,
        }, opts, cancellationToken), isWrite: false, "RetrieveOrder", cancellationToken);

        return GetString(root, "status") switch
        {
            "FILLED" => ProviderOrderOutcome.Settled,
            "CANCELLED" => ProviderOrderOutcome.Failed,
            _ => ProviderOrderOutcome.Pending,
        };
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private UserCreateRequest BuildUserBody(InvestorSignUpDetails d)
    {
        var address = new Address
        {
            AddressLine1 = d.AddressLine1,
            Postcode = d.Postcode,
            City = d.City,
            Country = ResolveCountry(d.Country, "Country"),
        };
        var byol = new UserByolCreateRequest
        {
            FirstName = d.FirstName,
            LastName = d.LastName,
            Email = d.Email,
            BirthDate = d.BirthDate,
            Nationalities = new[] { ResolveNationality(d.Nationality) },
            Address = address,
        };
        if (!string.IsNullOrWhiteSpace(d.PhoneNumber))
            byol.AdditionalProperties.Set("phone_number", d.PhoneNumber);
        return byol;
    }

    private static Country ResolveCountry(string alpha2, string field)
    {
        if (Country.TryGetKnownValue(alpha2, out var country) && country is not null) return country;
        throw new UpvestGatewayException($"Unsupported country code '{alpha2}' for {field}.", statusCode: 400);
    }

    private static Nationality ResolveNationality(string alpha2)
    {
        if (Nationality.TryGetKnownValue(alpha2, out var nationality) && nationality is not null) return nationality;
        throw new UpvestGatewayException($"Unsupported nationality code '{alpha2}'.", statusCode: 400);
    }

    private static string FormatMoney(decimal amount) =>
        amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static Guid GetGuid(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.TryGetGuid(out var g)
            ? g
            : throw new UpvestGatewayException($"Provider response did not contain a valid '{name}'.");

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? string.Empty
            : string.Empty;

    private static ProviderInvestorStatus MapInvestorStatus(string status) => status.ToUpperInvariant() switch
    {
        "ACTIVE" => ProviderInvestorStatus.Active,
        "REJECTED" or "OFFBOARDED" or "DECEASED" => ProviderInvestorStatus.Rejected,
        _ => ProviderInvestorStatus.Pending,
    };

    /// <summary>
    /// Invokes an SDK operation, captures the raw response via a per-call hook, and returns the parsed JSON.
    /// Tolerates a 2xx body that the strict model rejects; translates every error to
    /// <see cref="UpvestGatewayException"/>.
    /// </summary>
    private static async Task<JsonElement> CallAsync(Func<RequestOptions, Task> invoke, bool isWrite, string op, CancellationToken cancellationToken)
    {
        string? rawBody = null;
        HttpStatusCode statusCode = 0;
        var options = new RequestOptions
        {
            Hooks = new[]
            {
                SdkHook.OnResponse(async (response, _, ct) =>
                {
                    statusCode = response.StatusCode;
                    if (response.Content is not null)
                    {
                        await response.Content.LoadIntoBufferAsync();
                        rawBody = await response.Content.ReadAsStringAsync(ct);
                    }
                }),
            },
        };

        try
        {
            await invoke(options);
        }
        catch (ResponseDeserializationException ex)
        {
            // Only the success path produces this; a non-2xx status here would be a genuine error.
            if ((int)ex.StatusCode is < 200 or >= 300)
                throw Translate(ex, op, isWrite);
            // 2xx with a body the strict model rejects — fall through and read the captured body.
        }
        catch (ApiException ex)
        {
            throw Translate(ex, op, isWrite);
        }
        catch (AuthSchemeException ex)
        {
            throw new UpvestGatewayException($"{op}: provider credentials could not be applied.", ex);
        }
        catch (SdkTimeoutException ex)
        {
            throw new UpvestGatewayException($"{op}: provider did not answer in time.", ex, outcomeUnknown: isWrite);
        }
        catch (SdkConnectionException ex)
        {
            throw new UpvestGatewayException($"{op}: provider was unreachable.", ex, outcomeUnknown: isWrite);
        }

        if (string.IsNullOrEmpty(rawBody))
            throw new UpvestGatewayException($"{op}: provider returned no response body.", outcomeUnknown: isWrite);

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new UpvestGatewayException($"{op}: provider response was not valid JSON.", ex);
        }
    }

    private static UpvestGatewayException Translate(ApiException ex, string op, bool isWrite) =>
        new($"{op}: provider returned {(int)ex.StatusCode}.", ex, statusCode: (int)ex.StatusCode, outcomeUnknown: false);
}
