using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Exceptions;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.AccountGroups;
using UpvestInvestmentApi.Requests.AccountsApi;
using UpvestInvestmentApi.Requests.Orders;
using UpvestInvestmentApi.Requests.Users;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// The Upvest-backed implementation of <see cref="IUpvestInvestorGateway"/>. Translates domain operations
/// into SDK calls and provider failures into <see cref="UpvestProviderException"/>. Never logs personal
/// data — only ids, amounts and statuses.
/// </summary>
public sealed class UpvestInvestorGateway : IUpvestInvestorGateway
{
    // A syntactically valid bearer placeholder: the SDK validates the Authorization header value when it
    // builds the request (an empty string throws FormatException), so call sites set this and the single
    // authentication handler overwrites it with the real token on the wire. No real credential here.
    private const string PlaceholderAuthorization = "Bearer placeholder";

    private readonly UpvestInvestmentApiClient _client;
    private readonly UpvestOptions _options;
    private readonly ILogger<UpvestInvestorGateway> _logger;
    private readonly Guid _clientId;

    public UpvestInvestorGateway(
        UpvestInvestmentApiClient client,
        IOptions<UpvestOptions> options,
        ILogger<UpvestInvestorGateway> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        _clientId = Guid.Parse(_options.ClientId);
    }

    public async Task<InvestorProvisionResult> ProvisionInvestorAsync(
        string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken)
    {
        var user = await CallAsync("CreateUser", ct => _client.Users.CreateUser(new CreateUserRequest
        {
            UpvestClientId = _clientId,
            Authorization = PlaceholderAuthorization,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = DeterministicGuid(buyerId, "user"),
            Body = BuildUserBody(details),
        }, cancellationToken: ct), cancellationToken);

        if (!user.TryGetUserTol(out var tol))
            throw new UpvestProviderException("Provider returned an unexpected user shape.");
        var userId = tol.Id;

        var group = await CallAsync("CreateAccountGroup", ct => _client.AccountGroups.CreateAccountGroup(
            new CreateAccountGroupRequest
            {
                UpvestClientId = _clientId,
                Authorization = PlaceholderAuthorization,
                Signature = string.Empty,
                SignatureInput = string.Empty,
                IdempotencyKey = DeterministicGuid(buyerId, "group"),
                Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
                {
                    UserId = userId,
                    Type = Type13.Personal,
                }),
            }, cancellationToken: ct), cancellationToken);

        if (!group.TryGetAccountGroup(out var accountGroup))
            throw new UpvestProviderException("Provider returned an unexpected account-group shape.");
        var accountGroupId = accountGroup.Id;

        var account = await CallAsync("CreateAccount", ct => _client.AccountsApi.CreateAccount(
            new CreateAccountRequest
            {
                UpvestClientId = _clientId,
                Authorization = PlaceholderAuthorization,
                Signature = string.Empty,
                SignatureInput = string.Empty,
                IdempotencyKey = DeterministicGuid(buyerId, "account"),
                Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
                {
                    UserId = userId,
                    AccountGroupId = accountGroupId,
                    Type = Type16.Trading,
                    Name = "Spare change",
                }),
            }, cancellationToken: ct), cancellationToken);

        if (!account.TryGetAccount(out var acct))
            throw new UpvestProviderException("Provider returned an unexpected account shape.");

        _logger.LogInformation("Provisioned Upvest investor (account {AccountId}, status {Status}).",
            acct.Id, acct.Status.Value);

        return new InvestorProvisionResult(userId, accountGroupId, acct.Id, MapAccountStatus(acct.Status));
    }

    public async Task<EnrolmentStatus> GetEnrolmentStatusAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var response = await CallAsync("RetrieveAccount", ct => _client.AccountsApi.RetrieveAccount(
            new RetrieveAccountRequest
            {
                AccountId = accountId,
                UpvestClientId = _clientId,
                Authorization = PlaceholderAuthorization,
                Signature = string.Empty,
                SignatureInput = string.Empty,
            }, cancellationToken: ct), cancellationToken);

        if (response.TryGetAccount(out var acct))
            return MapAccountStatus(acct.Status);

        // Unexpected shape: treat as still pending rather than rejecting a shopper.
        return EnrolmentStatus.Pending;
    }

    public async Task<InvestmentOrderResult> PlaceInvestmentOrderAsync(
        Guid userId, Guid accountId, long amountCents, Guid reference, CancellationToken cancellationToken)
    {
        var order = await CallAsync("PlaceOrder", ct => _client.Orders.PlaceOrder(new PlaceOrderRequest
        {
            UpvestClientId = _clientId,
            Authorization = PlaceholderAuthorization,
            Signature = string.Empty,
            SignatureInput = string.Empty,
            IdempotencyKey = reference,
            Body = new OrderPlaceRequest
            {
                UserId = userId,
                AccountId = accountId,
                Side = Side.Buy,
                InstrumentId = _options.InstrumentId,
                CashAmount = FormatEuros(amountCents),
                Currency = Currency29.Eur,
                ClientReference = reference.ToString(),
            },
        }, cancellationToken: ct), cancellationToken);

        _logger.LogInformation("Placed investment order {OrderId} for {Amount} ({Status}).",
            order.Id, FormatEuros(amountCents), order.Status.Value);
        return new InvestmentOrderResult(order.Id, MapOrderStatus(order.Status));
    }

    public async Task<InvestmentStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await CallAsync("RetrieveOrder", ct => _client.Orders.RetrieveOrder(new RetrieveOrderRequest
        {
            OrderId = orderId,
            UpvestClientId = _clientId,
            Authorization = PlaceholderAuthorization,
            Signature = string.Empty,
            SignatureInput = string.Empty,
        }, cancellationToken: ct), cancellationToken);

        return MapOrderStatus(order.Status);
    }

    public async Task<InvestmentOrderResult?> FindOrderByReferenceAsync(
        Guid accountId, Guid reference, CancellationToken cancellationToken)
    {
        var wanted = reference.ToString();
        const int pageSize = 100;
        const int maxPages = 50; // bound the walk; never loop on the provider's say-so alone
        var offset = 0;

        for (var page = 0; page < maxPages; page++)
        {
            var currentOffset = offset;
            var response = await CallAsync("ListAccountOrders", ct => _client.Orders.ListAccountOrders(
                new ListAccountOrdersRequest
                {
                    AccountId = accountId,
                    UpvestClientId = _clientId,
                    Authorization = PlaceholderAuthorization,
                    Signature = string.Empty,
                    SignatureInput = string.Empty,
                    Offset = currentOffset,
                    Limit = pageSize,
                }, cancellationToken: ct), cancellationToken);

            foreach (var order in response.Data)
            {
                if (string.Equals(order.ClientReference, wanted, StringComparison.Ordinal))
                    return new InvestmentOrderResult(order.Id, MapOrderStatus(order.Status));
            }

            offset += response.Data.Count == 0 ? pageSize : response.Data.Count;
            if (response.Data.Count == 0 || offset >= response.Meta.TotalCount)
                return null; // exhausted: no order carries this reference
        }

        // Hit the page cap without resolving: report unknown rather than a false "not found".
        throw new UpvestProviderException(
            "Could not resolve the order by reference within the page limit.") { OutcomeUnknown = true };
    }

    private UserCreateRequest BuildUserBody(InvestorEnrolmentDetails details)
    {
        if (!Nationality.TryGetKnownValue(details.Nationality, out var nationality))
            throw new UpvestProviderException($"Unsupported nationality code '{details.Nationality}'.");
        if (!Country.TryGetKnownValue(details.Country, out var country))
            throw new UpvestProviderException($"Unsupported country code '{details.Country}'.");

        return UserCreateRequest.UserTolCreateRequest(new UserTolCreateRequest
        {
            FirstName = details.FirstName,
            LastName = details.LastName,
            Email = details.Email,
            BirthDate = new DateTimeOffset(details.BirthDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            Nationalities = new[] { nationality },
            PhoneNumber = string.IsNullOrWhiteSpace(details.PhoneNumber) ? null : details.PhoneNumber,
            Address = new Address
            {
                AddressLine1 = details.AddressLine1,
                Postcode = details.Postcode,
                City = details.City,
                Country = country,
            },
            Fatca = new Fatca { Status = false, ConfirmedAt = DateTimeOffset.UtcNow },
        });
    }

    private static EnrolmentStatus MapAccountStatus(Status21 status) => status.Match(
        onPendingApproval: () => EnrolmentStatus.Pending,
        onActive: () => EnrolmentStatus.Active,
        onClosing: () => EnrolmentStatus.Rejected,
        onClosed: () => EnrolmentStatus.Rejected,
        onLocked: () => EnrolmentStatus.Rejected,
        otherwise: _ => EnrolmentStatus.Pending);

    private static InvestmentStatus MapOrderStatus(Status51 status) => status.Match(
        onNew: () => InvestmentStatus.Pending,
        onProcessing: () => InvestmentStatus.Pending,
        onFilled: () => InvestmentStatus.Settled,
        onCancelled: () => InvestmentStatus.Failed,
        otherwise: _ => InvestmentStatus.Pending);

    private static string FormatEuros(long cents) =>
        (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A stable GUID derived from the shopper id and a purpose, for idempotency keys.</summary>
    private static Guid DeterministicGuid(string buyerId, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"eshop-investing:{purpose}:{buyerId}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    /// <summary>
    /// Single error boundary: translates every SDK failure into <see cref="UpvestProviderException"/>.
    /// Connection/timeout failures are flagged <see cref="UpvestProviderException.OutcomeUnknown"/> so a
    /// write's caller can reconcile instead of assuming it failed. No personal data is logged.
    /// </summary>
    private async Task<T> CallAsync<T>(string operation, Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call(ct).ConfigureAwait(false);
        }
        catch (ApiException ex) // includes ApiException<TError> and ResponseDeserializationException
        {
            _logger.LogWarning("Upvest {Operation} returned {Status}.", operation, (int)ex.StatusCode);
            throw new UpvestProviderException($"Upvest {operation} failed.", ex.StatusCode, ex);
        }
        catch (AuthSchemeException ex)
        {
            _logger.LogError("Upvest {Operation}: credentials could not be applied.", operation);
            throw new UpvestProviderException($"Upvest {operation}: credentials could not be applied.", null, ex);
        }
        catch (SdkException ex) // connection / timeout: the outcome of a write is unknown
        {
            _logger.LogWarning("Upvest {Operation} could not be completed ({Type}).", operation, ex.GetType().Name);
            throw new UpvestProviderException($"Upvest {operation}: provider unreachable.", null, ex)
            {
                OutcomeUnknown = true,
            };
        }
    }
}
