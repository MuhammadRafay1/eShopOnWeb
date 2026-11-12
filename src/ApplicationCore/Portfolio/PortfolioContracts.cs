using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>The shop's investor sign-up form, SDK-free. Personal details; never logged.</summary>
public record InvestorSignup(
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
    IdentityDocumentInput IdentityDocument);

/// <summary>A residential address on the sign-up form.</summary>
public record InvestorAddress(string Line1, string Postcode, string City, string Country);

/// <summary>The identity document the shop verified the shopper with.</summary>
public record IdentityDocumentInput(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

/// <summary>Outcome of provisioning an investor at Upvest.</summary>
public record InvestorProvisionResult(
    Guid UpvestUserId,
    Guid? AccountGroupId,
    Guid? AccountId,
    InvestorLifecycleStatus Status);

/// <summary>One fund of a model, as it crosses the gateway boundary.</summary>
public record ModelFundInput(string InstrumentId, decimal Weight);

/// <summary>Everything the gateway needs to credit cash and place an invest order.</summary>
public record InvestorPayIn(
    Guid UserId,
    Guid AccountGroupId,
    Guid AccountId,
    string OwnerName,
    Guid? VirtualBankAccountId,
    decimal Amount,
    Guid VirtualBankAccountIdempotencyKey,
    Guid MoneyInIdempotencyKey,
    Guid OrderIdempotencyKey);

/// <summary>Outcome of placing an invest order (plus the virtual bank account used, created on first use).</summary>
public record PaymentPlacementResult(Guid UpvestOrderId, PaymentStatus Status, Guid VirtualBankAccountId);

/// <summary>One holding in the investor's portfolio as Upvest records it.</summary>
public record RemoteHolding(string InstrumentId, decimal Quantity, decimal Value);

/// <summary>The investor's portfolio as Upvest currently records it.</summary>
public record RemotePortfolio(Guid? ModelAllocationId, IReadOnlyList<RemoteHolding> Holdings, decimal TotalValue);
