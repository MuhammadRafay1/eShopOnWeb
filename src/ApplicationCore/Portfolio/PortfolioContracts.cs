using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

// ---------------------------------------------------------------------------------------------------------
// Domain-facing contracts for the Upvest portfolio gateway. These keep the Upvest SDK types out of the
// application/web layers: the gateway maps between these and the SDK. Personal data lives only in the
// sign-up form below, which is passed straight to the gateway and never persisted or logged.
// ---------------------------------------------------------------------------------------------------------

/// <summary>Derived investor sign-up status, as reported by the API (reflects Upvest state).</summary>
public enum InvestorStatus
{
    Pending,
    Active,
    Rejected
}

/// <summary>Derived pay-in status (reflects the Upvest portfolio-order outcome).</summary>
public enum PaymentStatus
{
    Pending,
    Invested,
    Failed
}

/// <summary>Derived realignment status (reflects the Upvest rebalancing-execution outcome).</summary>
public enum RealignmentStatus
{
    Pending,
    Completed,
    Failed
}

/// <summary>The shop's investor sign-up form. Carries personal data — never log or persist this.</summary>
public record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    SignUpAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    SignUpIdentityDocument IdentityDocument);

public record SignUpAddress(string Line1, string Postcode, string City, string Country);

public record SignUpIdentityDocument(string Type, string Number, string IssuingCountry, DateTimeOffset ExpiryDate);

/// <summary>Idempotency keys for the onboarding writes, generated once and persisted by the caller.</summary>
public record InvestorOnboardingKeys(Guid UserKey, Guid TaxKey, Guid AccountGroupKey, Guid AccountKey);

/// <summary>
/// Result of running the onboarding chain. Ids are nullable because the chain can stop early: in an
/// environment whose client lacks a scope (e.g. the sandbox beyond user operations) a later step returns
/// 401, which is captured as <see cref="IncompleteReason"/> rather than thrown.
/// </summary>
public record InvestorOnboardingResult(
    Guid UserId,
    Guid? CheckId,
    Guid? AccountGroupId,
    Guid? AccountId,
    string? IncompleteReason);

/// <summary>A fund target within a model: ISIN and weight percentage.</summary>
public record FundWeight(string InstrumentId, decimal Weight);

/// <summary>A model as Upvest records it (a portfolio allocation).</summary>
public record ModelPortfolioResult(Guid AllocationId, string Name, IReadOnlyList<FundWeight> Funds);

/// <summary>Result of placing a pay-in (portfolio order).</summary>
public record PayInResult(Guid PortfolioOrderId, PaymentStatus Status, Guid VirtualBankAccountId);

/// <summary>A single holding in the investor's portfolio.</summary>
public record Holding(string InstrumentId, decimal Quantity, decimal Value);

/// <summary>Snapshot of the investor's portfolio as Upvest currently records it.</summary>
public record PortfolioSnapshot(IReadOnlyList<Holding> Holdings, decimal TotalValue);
