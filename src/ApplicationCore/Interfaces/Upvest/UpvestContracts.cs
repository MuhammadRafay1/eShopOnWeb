using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

/// <summary>
/// The investor sign-up form the shop collected and verified, mapped onto what Upvest needs to
/// create a user and start onboarding. These values are personal data: they are sent to Upvest
/// but are never persisted by the shop and never written to logs.
/// </summary>
public record InvestorRegistration(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    InvestorAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    InvestorIdentityDocument IdentityDocument);

public record InvestorAddress(string Line1, string Postcode, string City, string Country);

public record InvestorIdentityDocument(string Type, string Number, string IssuingCountry, string ExpiryDate);

/// <summary>A fund within a model, as sent to Upvest. <see cref="WeightPercent"/> is 0–100.</summary>
public record FundWeight(string InstrumentId, decimal WeightPercent);

public record UpvestUser(string Id, string Status);

public record UpvestAccount(string Id, string Status);

public record UpvestOrder(string Id, string Status);

public record UpvestRebalancing(string Id, string Status);

public record UpvestHolding(string InstrumentId, decimal Quantity, decimal Value);

public record UpvestValuation(decimal TotalValue, string Currency, IReadOnlyList<UpvestHolding> Holdings);
