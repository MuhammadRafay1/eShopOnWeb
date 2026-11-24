using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

// ---------------------------------------------------------------------------
// Inputs and result views for the portfolio service. The field names that the
// task fixes are rendered at the API boundary (see the PublicApi response DTOs);
// these are the internal shapes the service produces.
// ---------------------------------------------------------------------------

/// <summary>The shop's investor sign-up form.</summary>
public record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    SignUpAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    SignUpIdentityDocument IdentityDocument);

public record SignUpAddress(string Line1, string Postcode, string City, string Country);

public record SignUpIdentityDocument(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

public record InvestorView(string InvestorId, string Status);

public record ModelFundInput(string InstrumentId, decimal Weight);

public record ModelView(string ModelId, string Name, IReadOnlyList<ModelFundInput> Funds);

public record PaymentView(string PaymentId, decimal Amount, string Status);

public record RealignmentView(string RealignmentId, string Status);

public record HoldingView(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioView(
    string? ModelId,
    IReadOnlyList<HoldingView> Holdings,
    decimal TotalValue,
    decimal PaidIn);
