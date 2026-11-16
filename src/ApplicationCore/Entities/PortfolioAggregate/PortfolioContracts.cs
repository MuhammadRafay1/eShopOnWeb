using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The shopper's investor sign-up details, as collected by the shop. This is transient personal data: it is
/// passed to the Upvest gateway for onboarding and is never persisted by this application nor written to logs.
/// </summary>
public record InvestorRegistration(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    RegistrationAddress Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocument IdentityDocument);

/// <summary>A residential address from the sign-up form.</summary>
public record RegistrationAddress(string Line1, string Postcode, string City, string Country);

/// <summary>The identity document the shop checked when it verified the shopper.</summary>
public record IdentityDocument(string Type, string Number, string IssuingCountry, DateTimeOffset ExpiryDate);

/// <summary>A fund and its target weight percentage within a model.</summary>
public record FundWeight(string InstrumentId, decimal Weight);

/// <summary>A model portfolio allocation as Upvest records it.</summary>
public record UpvestAllocation(Guid AllocationId, string Name, IReadOnlyList<FundWeight> Funds);

/// <summary>The result of placing a buy order for a pay-in.</summary>
public record UpvestOrderResult(Guid OrderId, PaymentStatus Status);

/// <summary>A single holding in a portfolio valuation.</summary>
public record Holding(string InstrumentId, decimal Quantity, decimal Value);

/// <summary>A portfolio's current holdings and total value, as Upvest records them.</summary>
public record PortfolioHoldings(IReadOnlyList<Holding> Holdings, decimal TotalValue, string Currency);

/// <summary>The investor's full portfolio view: model followed, holdings, total value and total paid in.</summary>
public record PortfolioView(
    string? ModelId,
    IReadOnlyList<Holding> Holdings,
    decimal TotalValue,
    decimal PaidIn,
    string Currency);
