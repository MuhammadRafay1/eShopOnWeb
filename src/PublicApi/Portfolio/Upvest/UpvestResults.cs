using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;

/// <summary>Personal details collected on the sign-up form, passed to the gateway to create a TOL user.</summary>
public sealed record InvestorSignupDetails(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    string AddressLine1,
    string Postcode,
    string City,
    string AddressCountry,
    string? PhoneNumber,
    string? TaxCountry);

/// <summary>A created/looked-up Upvest user.</summary>
public sealed record UpvestUserResult(Guid UserId, string Status);

/// <summary>A created Upvest account plus the group it belongs to.</summary>
public sealed record UpvestAccountResult(Guid AccountId, Guid AccountGroupId, string Status);

/// <summary>A created Upvest portfolio allocation (model portfolio).</summary>
public sealed record UpvestAllocationResult(Guid AllocationId);

/// <summary>A created Upvest top-up (cash-in).</summary>
public sealed record UpvestTopupResult(Guid TopupId, string? Status, string? SettlementReference);

/// <summary>A placed Upvest portfolio order.</summary>
public sealed record UpvestOrderResult(Guid OrderId, string Status);

/// <summary>One holding in an account valuation.</summary>
public sealed record UpvestHolding(string InstrumentId, decimal Quantity, decimal? Value);

/// <summary>An account valuation: holdings and the total security value.</summary>
public sealed record UpvestValuationResult(decimal TotalValue, IReadOnlyList<UpvestHolding> Holdings);

/// <summary>One fund weight in a model (ISIN + percentage) sent to Upvest.</summary>
public sealed record FundWeight(string InstrumentId, decimal Weight);
