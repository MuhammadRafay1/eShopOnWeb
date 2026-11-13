using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

// ─── Flow 1 — investor sign-up ────────────────────────────────────────────────────────────────

/// <summary>The shop's investor sign-up form, as checked by the shop when it verified the shopper.</summary>
public record InvestorSignupInput(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    AddressInput Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocumentInput IdentityDocument);

public record AddressInput(string Line1, string Postcode, string City, string Country);

public record IdentityDocumentInput(string Type, string Number, string IssuingCountry, DateTimeOffset ExpiryDate);

/// <summary>Where a shopper's sign-up has got to. <c>InvestorId</c> is the Upvest user id once created.</summary>
public record InvestorStatusResult(Guid? InvestorId, string Status);

// ─── Flow 2 — model portfolios ────────────────────────────────────────────────────────────────

public record ModelPortfolioInput(string Name, IReadOnlyList<FundInput> Funds);

public record FundInput(string InstrumentId, decimal Weight);

public record ModelPortfolioResult(string ModelId, string Name, IReadOnlyList<FundResult> Funds);

public record FundResult(string InstrumentId, decimal Weight);

// ─── Flow 3 — paying in ───────────────────────────────────────────────────────────────────────

public record PaymentResult(Guid PaymentId, decimal Amount, string Status);

// ─── Flow 4 — realignment ─────────────────────────────────────────────────────────────────────

public record RealignmentResult(Guid RealignmentId, string Status);

// ─── Flow 5 — the investor's portfolio ────────────────────────────────────────────────────────

public record PortfolioResult(
    string? ModelId,
    IReadOnlyList<HoldingResult> Holdings,
    decimal TotalValue,
    decimal PaidIn,
    bool Truncated = false);

public record HoldingResult(string InstrumentId, decimal Quantity, decimal Value);
