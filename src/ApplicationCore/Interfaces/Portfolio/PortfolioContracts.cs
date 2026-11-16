using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

// ---- Flow 1: becoming an investor ----

/// <summary>The shop's investor sign-up form. Carries the shopper's personal details and the verified
/// identity document the shop checked. These are sent to Upvest and never persisted or logged here.</summary>
public record InvestorSignupForm(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    InvestorAddress Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocumentForm IdentityDocument);

public record InvestorAddress(string Line1, string Postcode, string City, string Country);

public record IdentityDocumentForm(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

/// <summary>The investor's sign-up state as the caller sees it.</summary>
public record InvestorStatusResult(Guid InvestorId, InvestorSignupStatus Status);

// ---- Flow 2: model portfolios ----

public record ModelFundInput(string InstrumentId, decimal Weight);

public record ModelPortfolioDefinition(string Name, IReadOnlyList<ModelFundInput> Funds);

public record ModelPortfolioResult(string ModelId, string Name, IReadOnlyList<ModelFundInput> Funds);

// ---- Flow 3: paying in ----

public record PaymentResult(Guid PaymentId, decimal Amount, PaymentStatus Status);

// ---- Flow 4: realignment ----

public record RealignmentResult(Guid RealignmentId, RealignmentStatus Status);

// ---- Flow 5: the portfolio ----

public record PortfolioHolding(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioResult(
    string? ModelId,
    IReadOnlyList<PortfolioHolding> Holdings,
    decimal TotalValue,
    decimal PaidIn);
