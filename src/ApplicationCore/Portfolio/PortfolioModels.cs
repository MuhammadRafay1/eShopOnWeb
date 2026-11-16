using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>Where a sign-up / account provisioning has got to at Upvest.</summary>
public enum InvestorStatus
{
    /// <summary>Upvest has not yet accepted the shopper or their account is not yet ready.</summary>
    Pending,
    /// <summary>Upvest has accepted the shopper and their account is ready to hold a portfolio.</summary>
    Active,
    /// <summary>Upvest will not take the shopper on.</summary>
    Rejected
}

/// <summary>Where a pay-in has got to at Upvest.</summary>
public enum PaymentStatus
{
    Pending,
    Invested,
    Failed
}

/// <summary>Where a realignment has got to at Upvest.</summary>
public enum RealignmentStatus
{
    Pending,
    Completed,
    Failed
}

/// <summary>The shop's investor sign-up form and the identity document the shop verified.</summary>
public sealed record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    PostalAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocument IdentityDocument);

public sealed record PostalAddress(string Line1, string Postcode, string City, string Country);

public sealed record IdentityDocument(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

public sealed record InvestorView(Guid InvestorId, InvestorStatus Status);

/// <summary>A fund and its target weight (a percentage) inside a model portfolio.</summary>
public sealed record FundWeight(string InstrumentId, decimal Weight);

public sealed record ModelPortfolioView(string ModelId, string Name, IReadOnlyList<FundWeight> Funds);

public sealed record PaymentView(Guid PaymentId, decimal Amount, PaymentStatus Status);

public sealed record RealignmentView(Guid RealignmentId, RealignmentStatus Status);

/// <summary>One fund held in the portfolio, with its quantity and current value.</summary>
public sealed record Holding(string InstrumentId, decimal Quantity, decimal Value);

public sealed record PortfolioView(
    string? ModelId,
    IReadOnlyList<Holding> Holdings,
    decimal TotalValue,
    decimal PaidIn);
