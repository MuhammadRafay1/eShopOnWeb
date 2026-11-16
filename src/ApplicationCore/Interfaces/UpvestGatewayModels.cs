using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Plain domain types crossing the Upvest gateway boundary, so <c>ApplicationCore</c> never references
/// the Upvest SDK. The gateway implementation (in Infrastructure) translates these to and from SDK types.
/// </summary>

/// <summary>The shop's investor sign-up form.</summary>
public sealed record InvestorSignup(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    InvestorAddress Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    InvestorIdentityDocument IdentityDocument);

public sealed record InvestorAddress(string Line1, string Postcode, string City, string Country);

public sealed record InvestorIdentityDocument(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

/// <summary>Deterministic idempotency keys carried from the app's investor record.</summary>
public sealed record InvestorProvisioningKeys(
    Guid UserKey,
    Guid AccountGroupKey,
    Guid AccountKey);

/// <summary>The ids and mapped status produced by (re-)provisioning an investor at Upvest.</summary>
public sealed record InvestorProvisioning(
    Guid UserId,
    Guid? AccountGroupId,
    Guid? AccountId,
    InvestorStatus Status);

/// <summary>Current investor status as Upvest records it.</summary>
public sealed record InvestorStatusInfo(InvestorStatus Status);

/// <summary>A fund and its target weight (percentage) within a model.</summary>
public sealed record FundWeight(string InstrumentId, decimal Weight);

/// <summary>Everything needed to pay money in and invest it across the followed model.</summary>
public sealed record PayInCommand(
    Guid UserId,
    Guid AccountGroupId,
    Guid AccountId,
    decimal AmountEuros,
    string ClientReference,
    Guid TopupKey,
    Guid BankTransactionKey,
    Guid OrderKey);

/// <summary>The Upvest ids and mapped status produced by a pay-in.</summary>
public sealed record PayInResult(Guid TopupId, Guid? OrderId, PaymentStatus Status);

/// <summary>The caller's portfolio as Upvest currently records it.</summary>
public sealed record PortfolioSnapshot(
    Guid? AllocationId,
    IReadOnlyList<PortfolioHolding> Holdings,
    decimal TotalValue);

public sealed record PortfolioHolding(string InstrumentId, decimal Quantity, decimal Value);
