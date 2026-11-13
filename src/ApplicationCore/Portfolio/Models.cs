using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

// ---- Inputs the application layer receives from the API boundary ----

/// <summary>The shop's investor sign-up form, as checked by the shop.</summary>
public record InvestorSignupDetails(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    AddressDetails Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocumentDetails IdentityDocument);

public record AddressDetails(string Line1, string Postcode, string City, string Country);

public record IdentityDocumentDetails(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

/// <summary>A fund and its target weight (a percentage) within a model portfolio.</summary>
public record FundWeight(string InstrumentId, decimal Weight);

// ---- Results the application layer returns to the API boundary ----

public record InvestorStatusResult(Guid? InvestorId, InvestorSignupStatus Status);

public record ModelResult(string ModelId, string Name, IReadOnlyList<FundWeight> Funds);

public record PaymentResult(Guid PaymentId, decimal Amount, PaymentStatus Status);

public record RealignmentResult(Guid RealignmentId, RealignmentStatus Status);

public record HoldingResult(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioView(
    string? ModelId,
    IReadOnlyList<HoldingResult> Holdings,
    decimal TotalValue,
    decimal PaidIn);
