using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Everything the "invest your change" feature needs from Upvest, expressed in the application's own
/// terms. The implementation (in Infrastructure) is the only place that talks to the Upvest SDK, and
/// every call it makes is authenticated by the single reusable Upvest DelegatingHandler.
/// Personal data flows through the create call and is never logged.
/// </summary>
public interface IUpvestGateway
{
    Task<Guid> CreateInvestorAsync(UpvestInvestorDetails details, Guid idempotencyKey, CancellationToken ct);

    Task SubmitKycCheckAsync(Guid userId, CancellationToken ct);

    Task SetTaxResidencyAsync(Guid userId, string taxCountry, string taxId, Guid idempotencyKey, CancellationToken ct);

    Task<UpvestUserState> GetUserStateAsync(Guid userId, CancellationToken ct);

    Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken ct);

    Task<Guid> CreateTradingAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken ct);

    Task<UpvestAccountState> GetAccountStateAsync(Guid accountId, CancellationToken ct);

    /// <summary>Fund the account group with the given amount (euro cents). Returns the top-up id.</summary>
    Task<Guid> CreateTopupAsync(Guid accountGroupId, long amountCents, Guid idempotencyKey, CancellationToken ct);

    /// <summary>The account group's currently available cash, in euro cents (used to confirm funding landed).</summary>
    Task<long> GetAvailableCashCentsAsync(Guid accountGroupId, CancellationToken ct);

    /// <summary>The configured fund the change is invested in (Upvest:InstrumentId).</summary>
    string InstrumentId { get; }

    /// <summary>Place a nominal (cash-amount) BUY order for the configured fund. Returns the order id.</summary>
    Task<Guid> PlaceBuyOrderAsync(
        Guid accountId, long amountCents, string clientReference, Guid idempotencyKey, CancellationToken ct);

    Task<UpvestOrderState> GetOrderStateAsync(Guid orderId, CancellationToken ct);

    /// <summary>Ensure a webhook subscription exists pointing at this application's callback URL.</summary>
    Task EnsureWebhookAsync(string callbackUrl, CancellationToken ct);

    /// <summary>The webhook-signing public keys (JWKS) used to verify inbound Upvest deliveries.</summary>
    Task<UpvestWebhookKey[]> GetWebhookKeysAsync(CancellationToken ct);
}

/// <summary>The shop's investor sign-up form, mapped onto Upvest's user shape.</summary>
public sealed record UpvestInvestorDetails(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string Nationality,  // ISO 3166-1 alpha-2
    string AddressLine1,
    string Postcode,
    string City,
    string Country,      // ISO 3166-1 alpha-2
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,   // ISO 3166-1 alpha-2
    DateTimeOffset ConsentTimestamp);  // stable per enrolment, for idempotent replay

public enum UpvestUserState { Pending, Active, Rejected }
public enum UpvestAccountState { Pending, Active, Rejected }
public enum UpvestOrderState { Pending, Filled, Cancelled }

/// <summary>An EC public key from Upvest's webhook JWKS (P-521, base64url coordinates).</summary>
public sealed record UpvestWebhookKey(string Kid, string Curve, string X, string Y);

/// <summary>
/// A failure talking to Upvest, translated from the SDK's exception family. <see cref="IsTransient"/>
/// distinguishes a retryable condition (timeout, connection, 429, 5xx) from a terminal one
/// (a 4xx the caller/data caused) so the reconciler can decide whether to retry or reject.
/// </summary>
public sealed class UpvestGatewayException : Exception
{
    public bool IsTransient { get; }
    public int? StatusCode { get; }

    public UpvestGatewayException(string message, bool isTransient, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        IsTransient = isTransient;
        StatusCode = statusCode;
    }
}
