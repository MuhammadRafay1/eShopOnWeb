using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>
/// The shop's investor sign-up form, as collected from the shopper. These personal details are
/// passed to Upvest when the investor is created and are never persisted or logged by this app.
/// </summary>
public sealed record InvestorRegistration(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    AddressInfo Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocument IdentityDocument);

public sealed record AddressInfo(string Line1, string Postcode, string City, string Country);

/// <summary>The identity document the shop checked when it verified the shopper.</summary>
public sealed record IdentityDocument(string Type, string Number, string IssuingCountry, string ExpiryDate);

/// <summary>A fund in a model portfolio: ISIN and target weight (a percentage).</summary>
public sealed record ModelFund(string InstrumentId, decimal Weight);

/// <summary>Result of creating an investable account at Upvest.</summary>
public sealed record AccountProvisioned(System.Guid AccountId, bool Active);

/// <summary>
/// What Upvest currently records about a sign-up, read back by this application through its own calls.
/// </summary>
public sealed record InvestorAcceptance(bool KycPassed, bool KycFailed, bool AccountActive, bool UserOffboarded);

/// <summary>Outcome of an asynchronous Upvest operation (order or realignment).</summary>
public enum ProviderOutcome
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}

/// <summary>A snapshot of what an investor's portfolio holds at Upvest and what it is worth.</summary>
public sealed record PortfolioSnapshot(IReadOnlyList<HoldingSnapshot> Holdings, decimal TotalValue);

public sealed record HoldingSnapshot(string InstrumentId, decimal Quantity, decimal Value);
