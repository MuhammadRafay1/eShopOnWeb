using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.ManagedPortfolio;

/// <summary>
/// The shop's investor sign-up form, free of any Upvest SDK type. The provider layer translates it into the
/// Upvest user/identity/tax/account chain. Carries only what the shop collected; no values are logged.
/// </summary>
public record InvestorSignup(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    InvestorAddress Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    InvestorIdentityDocument IdentityDocument);

public record InvestorAddress(string Line1, string Postcode, string City, string Country);

/// <summary>
/// The identity document the shop checked when it verified the shopper. <see cref="ExpiryDate"/> is
/// validated at the API boundary (the document must not be expired) but is not forwarded to Upvest: the
/// Upvest identifier model has no expiry field (see the integration plan, Blocker B3).
/// </summary>
public record InvestorIdentityDocument(string Type, string Number, string IssuingCountry, DateTimeOffset ExpiryDate);

/// <summary>A single fund weighting of a model portfolio: a fund ISIN and its target percentage.</summary>
public record ModelFundSpec(string InstrumentId, decimal Weight);

/// <summary>
/// The identifiers and status produced by running the Upvest onboarding chain, as far as the sandbox scope
/// allowed. <see cref="UserId"/> is the Upvest user id (the investorId the API returns).
/// </summary>
public record InvestorOnboardingResult(
    Guid? UserId,
    Guid? AccountGroupId,
    Guid? AccountId,
    InvestorStatus Status);

/// <summary>The outcome of placing a pay-in: the Upvest references obtained and the derived status.</summary>
public record PayInResult(Guid? BankTransactionId, Guid? OrderId, PaymentStatus Status);

/// <summary>A single holding in the investor's portfolio as Upvest currently records it.</summary>
public record PortfolioHolding(string InstrumentId, decimal Quantity, decimal? Value);

/// <summary>The investor's portfolio as Upvest currently records it (model id and paid-in are added by the caller).</summary>
public record PortfolioSnapshot(IReadOnlyList<PortfolioHolding> Holdings, decimal TotalValue);
