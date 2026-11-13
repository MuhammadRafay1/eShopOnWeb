using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

/// <summary>
/// The shop's investor sign-up details, passed to Upvest to onboard the shopper. This record is
/// never persisted or logged by the application — it is forwarded to Upvest only.
/// </summary>
public record InvestorRegistration(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    RegistrationAddress Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    RegistrationIdentityDocument IdentityDocument);

/// <summary>A postal address on the sign-up form.</summary>
public record RegistrationAddress(string Line1, string Postcode, string City, string Country);

/// <summary>The identity document the shop checked when it verified the shopper.</summary>
public record RegistrationIdentityDocument(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

/// <summary>Where the investor's sign-up has got to at Upvest.</summary>
public enum UpvestUserStatus
{
    Unknown = 0,
    Active = 1,
    NotActive = 2
}

/// <summary>Whether the investor's account at Upvest is ready to hold a managed portfolio.</summary>
public enum UpvestAccountStatus
{
    Unknown = 0,
    Pending = 1,
    Active = 2,
    Closed = 3
}

/// <summary>Where a payment leg (cash-in or invest order) has got to at Upvest.</summary>
public enum UpvestLegStatus
{
    Pending = 0,
    Settled = 1,
    Failed = 2
}

/// <summary>Where a realignment has got to at Upvest.</summary>
public enum UpvestRealignmentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}

/// <summary>A fund and its target weight (percentage) within a model.</summary>
public record FundWeight(string InstrumentId, decimal Weight);

/// <summary>A Upvest portfolio allocation (the provider-side representation of a model).</summary>
public record AllocationSummary(Guid AllocationId, string? Name, IReadOnlyList<FundWeight> Funds);

/// <summary>A position at Upvest: a held instrument and its quantity.</summary>
public record Position(string InstrumentId, decimal Quantity);

/// <summary>One fund held in a portfolio, with its quantity and current value.</summary>
public record Holding(string InstrumentId, decimal Quantity, decimal Value);

/// <summary>The current valuation of an account's portfolio at Upvest.</summary>
public record PortfolioValuation(decimal TotalValue, string Currency, IReadOnlyDictionary<string, decimal> PerInstrumentValue);
