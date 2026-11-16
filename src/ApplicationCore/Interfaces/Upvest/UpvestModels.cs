using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

/// <summary>The shop's investor sign-up form. Carries personal data in transit only — never logged or persisted.</summary>
public record InvestorSignup(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    InvestorAddress Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocument IdentityDocument);

public record InvestorAddress(string Line1, string Postcode, string City, string Country);

public record IdentityDocument(string Type, string Number, string IssuingCountry, string ExpiryDate);

/// <summary>A generic Upvest resource with an id and an optional status (user, account, order, top-up, ...).</summary>
public record UpvestResource(string Id, string? Status);

public record UpvestAllocationFund(string InstrumentId, string InstrumentIdType, string Weight);

public record UpvestAllocation(string Id, string? Name, IReadOnlyList<UpvestAllocationFund> Funds);

public record UpvestPosition(string InstrumentId, decimal Quantity, decimal? Value, string? Currency);

public record UpvestValuation(decimal TotalValue, string Currency, IReadOnlyList<UpvestPosition> Positions);

public record UpvestRebalancingOrder(string Id, string Status);
