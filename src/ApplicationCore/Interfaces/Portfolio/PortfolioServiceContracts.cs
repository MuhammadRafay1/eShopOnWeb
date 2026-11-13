using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

/// <summary>The shop's investor sign-up form, as received from a shopper.</summary>
public record InvestorSignUpForm(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    AddressForm Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocumentForm IdentityDocument);

public record AddressForm(string Line1, string Postcode, string City, string Country);

public record IdentityDocumentForm(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

/// <summary>The composed portfolio view returned by <c>GET /api/portfolio</c>.</summary>
public record InvestorPortfolio(
    string? ModelId,
    IReadOnlyList<PortfolioHolding> Holdings,
    decimal TotalValue,
    decimal PaidIn);
