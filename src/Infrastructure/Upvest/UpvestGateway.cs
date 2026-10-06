using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.UserChecks;
using UpvestInvestmentApi.Requests.UserIdentifiers;
using UpvestInvestmentApi.Requests.Users;
using UpvestInvestmentApi.Requests.WebhookSubscriptions;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// The Upvest implementation of <see cref="IUpvestGateway"/>. Authentication (bearer + HTTP signature) is
/// applied centrally by <see cref="UpvestAuthenticationHandler"/> for every call — both the SDK calls and the
/// raw calls below — so no call here attaches credentials.
/// <para>
/// User, check, tax and webhook calls use the Upvest .NET SDK. The account-group, account, funding and order
/// calls are issued as raw signed JSON through the same authenticated pipeline: the sandbox returns leaner
/// bodies than the SDK's generated response models (which mark fields such as <c>users</c> required), so the
/// SDK cannot deserialize those responses even though the writes succeed. The SDK remains the reference for
/// the routes, payload shapes and auth; only the response parsing is done here.
/// </para>
/// Nationalities in the CONCAT set (AT, DE, FR, HU, IE, LU) do not require a separate regulatory identifier.
/// </summary>
public sealed class UpvestGateway : IUpvestGateway
{
    private static readonly System.Collections.Generic.IReadOnlySet<string> ConcatNationalities =
        new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "AT", "DE", "FR", "HU", "IE", "LU" };

    // Placeholders — the authentication handler supplies the real tenant id and signature material. They must
    // be non-empty and format-valid because the SDK adds them as headers (with validation) before the handler
    // runs; the handler then replaces them with the real values.
    private static readonly Guid NoClientId = Guid.Empty;
    private const string AuthorizationPlaceholder = "Bearer placeholder";
    private const string SignaturePlaceholder = "placeholder";

    private readonly UpvestInvestmentApiClient _client;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpvestSettings _settings;
    private readonly ILogger<UpvestGateway> _logger;

    public UpvestGateway(
        UpvestInvestmentApiClient client,
        IHttpClientFactory httpClientFactory,
        IOptions<UpvestSettings> settings,
        ILogger<UpvestGateway> logger)
    {
        _client = client;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<UpvestInvestorRegistration> RegisterInvestorAsync(
        InvestorEnrolmentData data, CancellationToken cancellationToken)
    {
        var userId = await CreateUserAsync(data, cancellationToken);
        await SubmitKycCheckAsync(userId, cancellationToken);
        await SetTaxResidencyAsync(userId, data, cancellationToken);
        await CreateIdentifierIfRequiredAsync(userId, data, cancellationToken);
        var webhookId = await SubscribeWebhookAsync(cancellationToken);

        return new UpvestInvestorRegistration { UpvestUserId = userId, WebhookId = webhookId };
    }

    public async Task<UpvestAccountProvision> ProvisionAccountAsync(
        Guid upvestUserId, CancellationToken cancellationToken)
    {
        using var group = await RawSendAsync(
            HttpMethod.Post, "/account_groups",
            new { user_id = upvestUserId, type = "PERSONAL" },
            Guid.NewGuid(), "create account group", isWrite: true, cancellationToken);
        var accountGroupId = group.RootElement.GetProperty("id").GetGuid();

        using var account = await RawSendAsync(
            HttpMethod.Post, "/accounts",
            new { user_id = upvestUserId, account_group_id = accountGroupId, type = "TRADING", name = "eShop spare change" },
            Guid.NewGuid(), "create account", isWrite: true, cancellationToken);
        var accountId = account.RootElement.GetProperty("id").GetGuid();
        var accountStatus = account.RootElement.GetProperty("status").GetString();

        return new UpvestAccountProvision
        {
            AccountGroupId = accountGroupId,
            AccountId = accountId,
            Status = MapAccountStatus(accountStatus)
        };
    }

    public async Task<EnrolmentStatus> GetAccountStatusAsync(Guid accountId, CancellationToken cancellationToken)
    {
        using var account = await RawSendAsync(
            HttpMethod.Get, $"/accounts/{accountId}", body: null, idempotencyKey: null,
            "retrieve account", isWrite: false, cancellationToken);
        return MapAccountStatus(account.RootElement.GetProperty("status").GetString());
    }

    public async Task<UpvestInvestmentPlacement> PlaceInvestmentAsync(
        UpvestInvestmentInstruction instruction, CancellationToken cancellationToken)
    {
        var amount = FormatAmount(instruction.Amount);

        // Fund the account group so the nominal buy order can be filled. Distinct idempotency key from the
        // order (the provider's idempotency store is keyed globally, so reusing the key with a different body
        // would be rejected).
        using (await RawSendAsync(
            HttpMethod.Post, "/virtual_cash_balances/increases",
            new { account_group_id = instruction.AccountGroupId, amount, currency = "EUR" },
            DeriveKey(instruction.IdempotencyKey, "fund"), "fund account group", isWrite: true, cancellationToken))
        {
        }

        using var order = await RawSendAsync(
            HttpMethod.Post, "/orders",
            new
            {
                user_id = instruction.UpvestUserId,
                account_id = instruction.AccountId,
                side = "BUY",
                instrument_id = _settings.InstrumentId,
                instrument_id_type = "ISIN",
                cash_amount = amount,
                currency = "EUR",
                order_type = "MARKET",
                user_instrument_fit_acknowledgement = true,
                client_reference = instruction.ClientReference
            },
            instruction.IdempotencyKey, "place order", isWrite: true, cancellationToken);

        return new UpvestInvestmentPlacement
        {
            UpvestOrderId = order.RootElement.GetProperty("id").GetGuid(),
            InitialStatus = MapOrderStatus(order.RootElement.GetProperty("status").GetString())
        };
    }

    public async Task<InvestmentStatus> GetInvestmentStatusAsync(
        Guid upvestOrderId, CancellationToken cancellationToken)
    {
        using var order = await RawSendAsync(
            HttpMethod.Get, $"/orders/{upvestOrderId}", body: null, idempotencyKey: null,
            "retrieve order", isWrite: false, cancellationToken);
        return MapOrderStatus(order.RootElement.GetProperty("status").GetString());
    }

    public async Task<UpvestInvestmentPlacement?> FindInvestmentByReferenceAsync(
        Guid accountId, string clientReference, CancellationToken cancellationToken)
    {
        using var orders = await RawSendAsync(
            HttpMethod.Get, $"/accounts/{accountId}/orders", body: null, idempotencyKey: null,
            "list account orders", isWrite: false, cancellationToken);

        if (!orders.RootElement.TryGetProperty("data", out var data))
        {
            return null;
        }

        foreach (var order in data.EnumerateArray())
        {
            if (order.TryGetProperty("client_reference", out var cref)
                && cref.GetString() == clientReference)
            {
                return new UpvestInvestmentPlacement
                {
                    UpvestOrderId = order.GetProperty("id").GetGuid(),
                    InitialStatus = MapOrderStatus(order.GetProperty("status").GetString())
                };
            }
        }

        return null;
    }

    // ---- SDK-based onboarding calls -------------------------------------------------------------------

    private async Task<Guid> CreateUserAsync(InvestorEnrolmentData data, CancellationToken cancellationToken)
    {
        var body = new UserTolCreateRequest
        {
            FirstName = data.FirstName,
            LastName = data.LastName,
            Email = data.Email,
            BirthDate = data.BirthDate,
            Nationalities = new[] { ResolveNationality(data.Nationality) },
            Address = new Address
            {
                AddressLine1 = data.Address.Line1,
                Postcode = data.Address.Postcode,
                City = data.Address.City,
                Country = ResolveCountry(data.Address.Country)
            },
            Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow },
            PhoneNumber = string.IsNullOrWhiteSpace(data.PhoneNumber) ? null : data.PhoneNumber
        };

        var response = await ExecuteAsync(
            () => _client.Users.CreateUser(
                new CreateUserRequest
                {
                    UpvestClientId = NoClientId,
                    Authorization = AuthorizationPlaceholder,
                    Signature = SignaturePlaceholder,
                    SignatureInput = SignaturePlaceholder,
                    IdempotencyKey = Guid.NewGuid(),
                    Body = UserCreateRequest.UserTolCreateRequest(body)
                },
                cancellationToken: cancellationToken),
            "create user", isWrite: true);

        if (response.TryGetUserTol(out var tolUser))
        {
            return tolUser.Id;
        }

        if (response.TryGetUserByol(out var byolUser))
        {
            return byolUser.Id;
        }

        throw new UpvestGatewayException("Upvest create user returned an unexpected user type.");
    }

    private async Task SubmitKycCheckAsync(Guid userId, CancellationToken cancellationToken)
    {
        var kyc = new UserCheckKnowYourCustomerCreateRequest
        {
            CheckConfirmedAt = DateTimeOffset.UtcNow,
            DataDownloadLink = "https://eshoponweb.example/kyc/evidence",
            DocumentType = DocumentType3.Passport,
            Provider = "eShopOnWeb",
            Method = Method.ElectronicId
        };

        await ExecuteTolerantAsync(
            () => _client.UserChecks.CreateUserCheck(
                new CreateUserCheckRequest
                {
                    UserId = userId,
                    UpvestClientId = NoClientId,
                    Authorization = AuthorizationPlaceholder,
                    Signature = SignaturePlaceholder,
                    SignatureInput = SignaturePlaceholder,
                    Body = UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(kyc)
                },
                cancellationToken: cancellationToken),
            "create KYC check", isWrite: true);
    }

    private async Task SetTaxResidencyAsync(
        Guid userId, InvestorEnrolmentData data, CancellationToken cancellationToken)
    {
        var residency = TaxResidencyForCreateRequest.WithTaxIdentifierNumber(
            new WithTaxIdentifierNumber
            {
                Country = ResolveCountry(data.TaxCountry),
                TaxIdentifierNumber = data.TaxId
            });

        await ExecuteTolerantAsync(
            () => _client.TaxResidencies.SetTaxResidencies(
                new SetTaxResidenciesRequest
                {
                    UserId = userId,
                    UpvestClientId = NoClientId,
                    Authorization = AuthorizationPlaceholder,
                    Signature = SignaturePlaceholder,
                    SignatureInput = SignaturePlaceholder,
                    IdempotencyKey = Guid.NewGuid(),
                    Body = new TaxResidenciesSetRequest { TaxResidencies = new[] { residency } }
                },
                cancellationToken: cancellationToken),
            "set tax residencies", isWrite: true);
    }

    private async Task CreateIdentifierIfRequiredAsync(
        Guid userId, InvestorEnrolmentData data, CancellationToken cancellationToken)
    {
        if (ConcatNationalities.Contains(data.Nationality))
        {
            return;
        }

        var body = new IdentifierCreateRequest
        {
            IssuingCountry = ResolveIssuingCountry(data.Nationality),
            Type = Type7.NationalId,
            Identifier = data.TaxId
        };

        await ExecuteTolerantAsync(
            () => _client.UserIdentifiers.CreateIdentifier(
                new CreateIdentifierRequest
                {
                    UserId = userId,
                    UpvestClientId = NoClientId,
                    Authorization = AuthorizationPlaceholder,
                    Signature = SignaturePlaceholder,
                    SignatureInput = SignaturePlaceholder,
                    Body = body
                },
                cancellationToken: cancellationToken),
            "create user identifier", isWrite: true);
    }

    private async Task<Guid?> SubscribeWebhookAsync(CancellationToken cancellationToken)
    {
        try
        {
            var callbackUrl = $"{_settings.CallbackBaseUrl.TrimEnd('/')}/api/investing/upvest/webhook";
            var webhook = await ExecuteAsync(
                () => _client.WebhookSubscriptions.CreateWebhook(
                    new CreateWebhookRequest
                    {
                        UpvestClientId = NoClientId,
                        Authorization = AuthorizationPlaceholder,
                        Signature = SignaturePlaceholder,
                        SignatureInput = SignaturePlaceholder,
                        Body = new WebhookCreateRequest { Title = "eShopOnWeb change", Url = callbackUrl }
                    },
                    cancellationToken: cancellationToken),
                "create webhook", isWrite: true);

            return webhook.Id;
        }
        catch (UpvestGatewayException)
        {
            // Webhooks are a convenience for push settlement; the integration also polls, so enrolment must
            // not fail if the subscription cannot be created.
            return null;
        }
    }

    private Nationality ResolveNationality(string code) =>
        Nationality.TryGetKnownValue(code.ToUpperInvariant(), out var value) && value is not null
            ? value
            : throw new UpvestGatewayException($"'{code}' is not a recognised nationality code.");

    private Country ResolveCountry(string code) =>
        Country.TryGetKnownValue(code.ToUpperInvariant(), out var value) && value is not null
            ? value
            : throw new UpvestGatewayException($"'{code}' is not a recognised country code.");

    private IssuingCountry ResolveIssuingCountry(string code) =>
        IssuingCountry.TryGetKnownValue(code.ToUpperInvariant(), out var value) && value is not null
            ? value
            : throw new UpvestGatewayException($"'{code}' is not a recognised issuing-country code.");

    private static string FormatAmount(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.ToEven).ToString("F2", CultureInfo.InvariantCulture);

    private static EnrolmentStatus MapAccountStatus(string? status) => status switch
    {
        "ACTIVE" => EnrolmentStatus.Active,
        "CLOSED" or "LOCKED" => EnrolmentStatus.Rejected,
        _ => EnrolmentStatus.Pending
    };

    private static InvestmentStatus MapOrderStatus(string? status) => status switch
    {
        "FILLED" => InvestmentStatus.Settled,
        "CANCELLED" => InvestmentStatus.Failed,
        _ => InvestmentStatus.Pending
    };

    private static Guid DeriveKey(Guid baseKey, string salt)
    {
        var material = baseKey.ToByteArray().Concat(Encoding.UTF8.GetBytes(salt)).ToArray();
        return new Guid(MD5.HashData(material));
    }

    // ---- raw signed JSON for account/order/funding (see class remarks) --------------------------------

    private async Task<JsonDocument> RawSendAsync(
        HttpMethod method, string path, object? body, Guid? idempotencyKey,
        string operation, bool isWrite, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(UpvestServiceCollectionExtensions.HttpClientName);
        using var request = new HttpRequestMessage(method, _settings.BaseUrl.TrimEnd('/') + path);
        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        if (idempotencyKey is { } key)
        {
            request.Headers.TryAddWithoutValidation("idempotency-key", key.ToString());
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
            when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // No usable response. For a write the outcome is unknown and is reconciled later.
            _logger.LogWarning("Upvest '{Operation}' did not complete: {Message}", operation, ex.Message);
            throw new UpvestGatewayException($"Upvest '{operation}' did not complete.", null, ex)
            {
                OutcomeUnknown = isWrite
            };
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Upvest '{Operation}' failed with status {Status}.", operation, (int)response.StatusCode);
                throw new UpvestGatewayException($"Upvest '{operation}' failed.", response.StatusCode);
            }

            return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
        }
    }

    // ---- SDK error boundary ---------------------------------------------------------------------------

    private async Task ExecuteTolerantAsync(Func<Task> call, string operation, bool isWrite)
    {
        try
        {
            await call();
        }
        catch (ResponseDeserializationException ex)
            when (ex.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices)
        {
            // A 2xx whose body does not match the SDK model is still a successful write (body not used here).
            _logger.LogInformation(
                "Upvest '{Operation}' succeeded ({Status}); response body ignored.", operation, (int)ex.StatusCode);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Upvest '{Operation}' failed: {Message}", operation, ex.Message);
            throw new UpvestGatewayException($"Upvest '{operation}' failed.", ex.StatusCode, ex);
        }
        catch (SdkException ex)
        {
            _logger.LogWarning("Upvest '{Operation}' did not complete: {Message}", operation, ex.Message);
            throw new UpvestGatewayException($"Upvest '{operation}' did not complete.", null, ex)
            {
                OutcomeUnknown = isWrite
            };
        }
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> call, string operation, bool isWrite)
    {
        try
        {
            return await call();
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("Upvest '{Operation}' failed: {Message}", operation, ex.Message);
            throw new UpvestGatewayException($"Upvest '{operation}' failed.", ex.StatusCode, ex);
        }
        catch (SdkException ex)
        {
            _logger.LogWarning("Upvest '{Operation}' did not complete: {Message}", operation, ex.Message);
            throw new UpvestGatewayException($"Upvest '{operation}' did not complete.", null, ex)
            {
                OutcomeUnknown = isWrite
            };
        }
    }
}
