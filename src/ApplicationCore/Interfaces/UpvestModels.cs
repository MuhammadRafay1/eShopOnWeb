using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>The shop's investor sign-up form, as collected from the shopper.</summary>
public record InvestorSignUp(
    string FirstName,
    string LastName,
    string? Email,
    string BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    InvestorAddress Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocument IdentityDocument);

public record InvestorAddress(string Line1, string Postcode, string City, string Country);

public record IdentityDocument(string Type, string Number, string IssuingCountry, string ExpiryDate);

public record UpvestUser(Guid Id, string Status);

public record UpvestAccountGroup(Guid Id, string Status);

public record UpvestAccount(Guid Id, string Status);

public record AllocationFund(string Isin, decimal Weight);

public record UpvestAllocation(Guid Id, string Name, IReadOnlyList<AllocationFund> Funds);

public record UpvestPortfolioOrder(Guid Id, string Status);

public record UpvestVirtualCashIncrease(Guid Id, string Status);

/// <summary>A rebalancing execution plus the statuses of its underlying orders (for status derivation).</summary>
public record UpvestRebalancingExecution(Guid Id, IReadOnlyList<string> OrderStatuses);

public record UpvestHolding(string Isin, decimal Quantity, decimal Value);

public record UpvestValuation(decimal TotalValue, string Currency, IReadOnlyList<UpvestHolding> Holdings);
