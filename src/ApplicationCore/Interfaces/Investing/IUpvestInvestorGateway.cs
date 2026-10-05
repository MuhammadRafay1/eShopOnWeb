using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;

/// <summary>
/// The shop's view of the investment provider. All provider wire concerns (signing, auth, the SDK,
/// ret/ error translation) live behind this abstraction in the Infrastructure layer; the domain sees
/// only plain values. Implementations translate every provider/transport failure to
/// <see cref="UpvestGatewayException"/> (unknown-outcome transport failures carry
/// <see cref="UpvestGatewayException.OutcomeUnknown"/>).
/// </summary>
public interface IUpvestInvestorGateway
{
    /// <summary>Create the shopper as an investor with the provider. Returns the provider investor id and status.</summary>
    Task<InvestorCreated> CreateInvestorAsync(InvestorSignUpDetails details, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Submit the KYC check and tax residency that let the provider accept the shopper.</summary>
    Task SubmitOnboardingEvidenceAsync(Guid upvestUserId, InvestorSignUpDetails details, Guid taxIdempotencyKey, CancellationToken cancellationToken);

    /// <summary>Where the investor's acceptance has got to.</summary>
    Task<ProviderInvestorStatus> GetInvestorStatusAsync(Guid upvestUserId, CancellationToken cancellationToken);

    /// <summary>Create the account group that holds the shopper's cash. Requires an accepted investor.</summary>
    Task<Guid> CreateAccountGroupAsync(Guid upvestUserId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Create the trading account the shopper's orders are placed on.</summary>
    Task<Guid> CreateAccountAsync(Guid upvestUserId, Guid accountGroupId, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Whether the account is ready to trade on.</summary>
    Task<bool> IsAccountActiveAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Fund the account group with the given euro amount (idempotent on the supplied key).</summary>
    Task TopUpAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Place the buy order for the configured fund and return the provider order id (idempotent on the key).</summary>
    Task<Guid> PlaceInvestmentOrderAsync(Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>The current outcome of a placed order at the provider.</summary>
    Task<ProviderOrderOutcome> GetOrderOutcomeAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <summary>Result of creating an investor.</summary>
public readonly record struct InvestorCreated(Guid UpvestUserId, ProviderInvestorStatus Status);

/// <summary>Provider-neutral investor acceptance status.</summary>
public enum ProviderInvestorStatus
{
    Pending = 0,
    Active = 1,
    Rejected = 2
}

/// <summary>Provider-neutral order outcome.</summary>
public enum ProviderOrderOutcome
{
    Pending = 0,
    Settled = 1,
    Failed = 2
}

/// <summary>
/// The investor sign-up form the shop collects. Passed to the provider at enrolment and never persisted
/// or logged. <see cref="BirthDate"/> is a calendar date; country/nationality are ISO 3166-1 alpha-2.
/// </summary>
public sealed record InvestorSignUpDetails
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset BirthDate { get; init; }
    public required string Nationality { get; init; }
    public required string AddressLine1 { get; init; }
    public required string Postcode { get; init; }
    public required string City { get; init; }
    public required string Country { get; init; }
    public string? PhoneNumber { get; init; }
    public required string TaxId { get; init; }
    public required string TaxCountry { get; init; }
}
