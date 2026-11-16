using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

// ---------------------------------------------------------------------------
// Input contracts (what a caller supplies), kept free of transport concerns.
// ---------------------------------------------------------------------------

/// <summary>Postal address as captured on the investor sign-up form.</summary>
public record AddressInput(string Line1, string Postcode, string City, string Country);

/// <summary>The identity document the shop checked when it verified the shopper.</summary>
public record IdentityDocumentInput(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

/// <summary>The shop's investor sign-up form.</summary>
public record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    AddressInput Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocumentInput IdentityDocument);

/// <summary>A single fund target in a model portfolio (weight is a percentage).</summary>
public record FundWeightInput(string InstrumentId, decimal Weight);

// ---------------------------------------------------------------------------
// Output contracts (what an endpoint returns). Money is in euros.
// ---------------------------------------------------------------------------

public record InvestorView(Guid InvestorId, string Status);

public record ModelFundView(string InstrumentId, decimal Weight);

public record ModelView(int ModelId, string Name, IReadOnlyList<ModelFundView> Funds);

public record PaymentView(Guid PaymentId, decimal Amount, string Status);

public record RealignmentView(Guid RealignmentId, string Status);

public record HoldingView(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioView(
    int? ModelId,
    IReadOnlyList<HoldingView> Holdings,
    decimal TotalValue,
    decimal PaidIn);
