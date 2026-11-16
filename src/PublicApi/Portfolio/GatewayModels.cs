using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.Portfolio;

/// <summary>Lifecycle state of an investor sign-up, derived from what Upvest reports.</summary>
public enum InvestorState { Pending, Active, Rejected }

/// <summary>Lifecycle state of a pay-in, derived from what Upvest reports.</summary>
public enum PaymentState { Pending, Invested, Failed }

/// <summary>Lifecycle state of a realignment, derived from what Upvest reports.</summary>
public enum RealignmentState { Pending, Completed, Failed }

/// <summary>Personal details the shop collected on its sign-up form, passed to the gateway to
/// provision the investor at Upvest. Not persisted by the app; never logged.</summary>
public sealed record InvestorRegistration(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string? BirthCity,
    string BirthCountry,
    string Nationality,
    AddressInput Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocumentInput IdentityDocument);

public sealed record AddressInput(string Line1, string Postcode, string City, string Country);

public sealed record IdentityDocumentInput(string Type, string Number, string IssuingCountry, DateTimeOffset ExpiryDate);

/// <summary>A model portfolio as Upvest records it (an allocation).</summary>
public sealed record UpvestAllocation(Guid Id, string Name, IReadOnlyList<UpvestFund> Funds);

public sealed record UpvestFund(string InstrumentId, decimal Weight);

/// <summary>Outcome of creating a top-up: the id, and the settlement reference used to settle it.</summary>
public sealed record TopupResult(Guid TopupId, string? SettlementReference);

/// <summary>The investor's portfolio as Upvest currently records it.</summary>
public sealed record UpvestPortfolio(IReadOnlyList<UpvestHolding> Holdings, decimal TotalValue);

public sealed record UpvestHolding(string InstrumentId, decimal Quantity, decimal Value);
