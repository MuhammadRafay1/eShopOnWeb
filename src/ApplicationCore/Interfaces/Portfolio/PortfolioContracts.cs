using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

/// <summary>
/// The sign-up form, as plain data handed to the Upvest boundary. Carries personal data — never log it.
/// </summary>
public record InvestorOnboardingData(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    string AddressLine1,
    string Postcode,
    string City,
    string Country,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>A fund and its target weight (percentage) within a model.</summary>
public record FundWeight(string Isin, decimal Weight);

/// <summary>
/// Result of checking a set of ISINs against Upvest's tradeable-instrument list.
/// <paramref name="Available"/> is false when the list could not be read (e.g. the client lacks
/// the <c>instruments:read</c> scope) — callers then degrade to format-only validation.
/// </summary>
public record InstrumentCheck(bool Available, IReadOnlyCollection<string> UnknownIsins);

/// <summary>One holding in an investor's portfolio as Upvest currently records it.</summary>
public record PortfolioHolding(string InstrumentId, decimal Quantity, decimal Value);

/// <summary>An investor's portfolio as Upvest currently records it.</summary>
public record PortfolioView(
    Guid AllocationId,
    IReadOnlyList<PortfolioHolding> Holdings,
    decimal TotalValue);
