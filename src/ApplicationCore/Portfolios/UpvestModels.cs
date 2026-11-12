using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolios;

// Clean, transport-agnostic models describing the slices of the Upvest Investment API this
// integration uses. The Infrastructure implementation maps these to/from Upvest's JSON.

/// <summary>A postal address, as Upvest expects it.</summary>
public record UpvestAddress(
    string AddressLine1,
    string Postcode,
    string City,
    string Country);

/// <summary>The identity document the shop verified, mapped onto Upvest's KYC check.</summary>
public record UpvestIdentityDocument(
    string Type,
    string Number,
    string IssuingCountry,
    DateOnly? ExpiryDate);

/// <summary>Everything needed to create an investor (user) at Upvest.</summary>
public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    UpvestAddress Address,
    string? PhoneNumber);

/// <summary>Result of creating or retrieving an Upvest user.</summary>
public record UpvestUser(string Id, string Status);

/// <summary>Result of creating or retrieving an Upvest account group.</summary>
public record UpvestAccountGroup(string Id, string Status);

/// <summary>Result of creating or retrieving an Upvest account.</summary>
public record UpvestAccount(string Id, string Status);

/// <summary>One fund weighting in an allocation. Weight is a fraction in [0,1] (Upvest's unit).</summary>
public record UpvestAllocationFund(string Isin, decimal Weight);

/// <summary>Result of creating/replacing/reading an Upvest portfolio allocation.</summary>
public record UpvestAllocation(string Id, string Name, IReadOnlyList<UpvestAllocationFund> Funds);

/// <summary>Result of creating a portfolio order.</summary>
public record UpvestPortfolioOrder(string Id, string Status);

/// <summary>Result of creating a top-up (cash credit).</summary>
public record UpvestTopup(string Id, string Status);

/// <summary>Result of creating a rebalancing execution.</summary>
public record UpvestRebalancingExecution(string Id);

/// <summary>One security position in an account valuation.</summary>
public record UpvestHolding(string InstrumentId, decimal Quantity, decimal Value);

/// <summary>An account's current valuation.</summary>
public record UpvestValuation(
    IReadOnlyList<UpvestHolding> Holdings,
    decimal TotalValue,
    string Currency);
