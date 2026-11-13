using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper who has signed up to hold a managed investment portfolio with Upvest.
/// Owns the shopper's sign-up form, the Upvest identifiers created on their behalf, and the
/// idempotency keys that make each onboarding write safely replayable.
/// <para>
/// The record belongs to one shopper (<see cref="BuyerId"/>); no shopper may ever see another's.
/// Personal details on this entity must never be written to logs.
/// </para>
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>Stable, opaque identifier surfaced to the caller as <c>investorId</c> (independent of Upvest).</summary>
    public Guid PublicId { get; private set; }

    // The shopper this investor record belongs to (the JWT identity / username). Unique.
    public string BuyerId { get; private set; }

    // --- Sign-up form (personal data — never logged) ---
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public DateOnly BirthDate { get; private set; }
    public string? BirthCity { get; private set; }
    public string? BirthCountry { get; private set; }
    public string Nationality { get; private set; }
    public string AddressLine1 { get; private set; }
    public string Postcode { get; private set; }
    public string City { get; private set; }
    public string Country { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string TaxId { get; private set; }
    public string TaxCountry { get; private set; }

    // Identity document the shop verified (persisted only; no in-scope Upvest operation accepts it).
    public string IdentityDocumentType { get; private set; }
    public string IdentityDocumentNumber { get; private set; }
    public string IdentityDocumentIssuingCountry { get; private set; }
    public DateOnly IdentityDocumentExpiryDate { get; private set; }

    // --- Upvest identifiers (filled progressively as onboarding steps succeed) ---
    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }
    public bool TaxResidenciesSet { get; private set; }

    // --- Model selection ---
    public string? SelectedModelId { get; private set; }
    public Guid? SelectedAllocationId { get; private set; }
    public bool ConfigurationCreated { get; private set; }

    // --- Idempotency keys (persisted so a resend replays the original Upvest write) ---
    public Guid UserIdempotencyKey { get; private set; }
    public Guid TaxIdempotencyKey { get; private set; }
    public Guid AccountGroupIdempotencyKey { get; private set; }
    public Guid AccountIdempotencyKey { get; private set; }

    // Last known status (authoritative value is re-derived from Upvest on read).
    public InvestorStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(
        string buyerId,
        string firstName,
        string lastName,
        string email,
        DateOnly birthDate,
        string? birthCity,
        string? birthCountry,
        string nationality,
        string addressLine1,
        string postcode,
        string city,
        string country,
        string? phoneNumber,
        string taxId,
        string taxCountry,
        string identityDocumentType,
        string identityDocumentNumber,
        string identityDocumentIssuingCountry,
        DateOnly identityDocumentExpiryDate)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        FirstName = Guard.Against.NullOrEmpty(firstName, nameof(firstName));
        LastName = Guard.Against.NullOrEmpty(lastName, nameof(lastName));
        Email = Guard.Against.NullOrEmpty(email, nameof(email));
        BirthDate = birthDate;
        BirthCity = birthCity;
        BirthCountry = birthCountry;
        Nationality = Guard.Against.NullOrEmpty(nationality, nameof(nationality));
        AddressLine1 = Guard.Against.NullOrEmpty(addressLine1, nameof(addressLine1));
        Postcode = Guard.Against.NullOrEmpty(postcode, nameof(postcode));
        City = Guard.Against.NullOrEmpty(city, nameof(city));
        Country = Guard.Against.NullOrEmpty(country, nameof(country));
        PhoneNumber = phoneNumber;
        TaxId = Guard.Against.NullOrEmpty(taxId, nameof(taxId));
        TaxCountry = Guard.Against.NullOrEmpty(taxCountry, nameof(taxCountry));
        IdentityDocumentType = Guard.Against.NullOrEmpty(identityDocumentType, nameof(identityDocumentType));
        IdentityDocumentNumber = Guard.Against.NullOrEmpty(identityDocumentNumber, nameof(identityDocumentNumber));
        IdentityDocumentIssuingCountry = Guard.Against.NullOrEmpty(identityDocumentIssuingCountry, nameof(identityDocumentIssuingCountry));
        IdentityDocumentExpiryDate = identityDocumentExpiryDate;

        PublicId = Guid.NewGuid();
        UserIdempotencyKey = Guid.NewGuid();
        TaxIdempotencyKey = Guid.NewGuid();
        AccountGroupIdempotencyKey = Guid.NewGuid();
        AccountIdempotencyKey = Guid.NewGuid();

        Status = InvestorStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestUser(Guid userId) => UpvestUserId = userId;
    public void MarkTaxResidenciesSet() => TaxResidenciesSet = true;
    public void SetAccountGroup(Guid accountGroupId) => UpvestAccountGroupId = accountGroupId;
    public void SetAccount(Guid accountId) => UpvestAccountId = accountId;

    public void SelectModel(string modelId, Guid? allocationId)
    {
        SelectedModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        SelectedAllocationId = allocationId;
        // A new model means any prior Upvest configuration no longer matches; it is re-derived on the next write.
    }

    public void MarkConfigurationCreated() => ConfigurationCreated = true;

    public void UpdateStatus(InvestorStatus status) => Status = status;

    /// <summary>True once Upvest has accepted the shopper and a model has been chosen — the pre-conditions for paying in.</summary>
    public bool CanPayIn => Status == InvestorStatus.Active
        && UpvestUserId.HasValue
        && UpvestAccountId.HasValue
        && UpvestAccountGroupId.HasValue
        && SelectedAllocationId.HasValue;
}
