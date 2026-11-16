using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>
/// The shop's investor sign-up form, plus the identity document the shop verified.
/// This is transient input: personal details are forwarded to Upvest and never persisted
/// or logged by this application.
/// </summary>
public sealed record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    PostalAddress Address,
    string? PhoneNumber,
    string? TaxId,
    string? TaxCountry,
    IdentityDocument IdentityDocument);

/// <summary>A postal address.</summary>
public sealed record PostalAddress(string Line1, string Postcode, string City, string Country);

/// <summary>The identity document the shop checked when it verified the shopper.</summary>
public sealed record IdentityDocument(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

/// <summary>
/// The local record tying a shopper to their Upvest investor entities. Holds only identifiers
/// returned by Upvest — never the shopper's personal details.
/// </summary>
public sealed class InvestorRecord
{
    public required string ShopperId { get; init; }
    public required string UpvestUserId { get; set; }
    public string? UpvestAccountGroupId { get; set; }
    public string? UpvestAccountId { get; set; }
    public string? SelectedModelId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>Set only when onboarding was definitively rejected during submission.</summary>
    public bool Rejected { get; set; }
}

/// <summary>A single fund within a model portfolio: its ISIN and its target weight (a percentage).</summary>
public sealed record ModelFund(string InstrumentId, decimal Weight);

/// <summary>An operator-defined model portfolio: a named mix of funds with target weights summing to 100.</summary>
public sealed class ModelPortfolio
{
    public required string ModelId { get; init; }
    public required string Name { get; set; }
    public required IReadOnlyList<ModelFund> Funds { get; set; }
    /// <summary>The Upvest allocation id backing this model.</summary>
    public string? UpvestAllocationId { get; set; }
}

/// <summary>A pay-in made by a shopper. Status is derived live from the Upvest portfolio order.</summary>
public sealed class PaymentRecord
{
    public required string PaymentId { get; init; }
    public required string ShopperId { get; init; }
    public required decimal Amount { get; init; }
    public string? UpvestOrderId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>Set when the order could not be placed at all (funding/order rejected up front).</summary>
    public bool SubmissionFailed { get; set; }
}

/// <summary>A realignment of all portfolios following a model. Status is derived live from Upvest.</summary>
public sealed class RealignmentRecord
{
    public required string RealignmentId { get; init; }
    public required string ModelId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool SubmissionFailed { get; set; }
}

// ----- Result / view types returned by IPortfolioService -----

public sealed record InvestorResult(string InvestorId, InvestorStatus Status);

public sealed record PaymentResult(string PaymentId, decimal Amount, PaymentStatus Status);

public sealed record RealignmentResult(string RealignmentId, RealignmentStatus Status);

public sealed record Holding(string InstrumentId, decimal Quantity, decimal Value);

public sealed record PortfolioView(
    string? ModelId,
    IReadOnlyList<Holding> Holdings,
    decimal TotalValue,
    decimal PaidIn);
