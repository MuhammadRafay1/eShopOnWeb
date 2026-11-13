using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Application-level data passed to and from <see cref="IUpvestInvestmentService"/>. These types deliberately
/// carry no Upvest SDK types, so the SDK stays an Infrastructure concern. Personal data here is used only to
/// build the Upvest request and is never logged.
/// </summary>
public record InvestorSignUp(
    string FirstName,
    string LastName,
    string? Email,
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

public record InvestorIdentityDocument(string Type, string Number, string IssuingCountry, DateTimeOffset ExpiryDate);

/// <summary>Outcome of provisioning an investor at Upvest, with whatever identifiers were created.</summary>
public record InvestorProvisioningResult(Guid UserId, Guid? AccountGroupId, Guid? AccountId, InvestorStatus Status);

/// <summary>A fund target within a model: its ISIN and its weight (percentage).</summary>
public record ModelFundInput(string InstrumentId, decimal Weight);

/// <summary>Outcome of a pay-in: the Upvest ids placed and the derived payment status.</summary>
public record PaymentPlacement(Guid? TopupId, Guid? OrderId, PaymentStatus Status);

/// <summary>A single holding in the investor's portfolio as Upvest records it.</summary>
public record Holding(string InstrumentId, decimal Quantity, decimal Value);

/// <summary>The investor's portfolio snapshot from Upvest: the allocation followed, holdings and total value.</summary>
public record PortfolioSnapshot(Guid? AllocationId, IReadOnlyList<Holding> Holdings, decimal TotalValue);
