using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Upvest;

/// <summary>Personal and identity details gathered on the shop's investor sign-up form.</summary>
public record UpvestUserRegistration(
    string FirstName,
    string LastName,
    string? Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    UpvestAddress Address,
    string? PhoneNumber);

public record UpvestAddress(string Line1, string Postcode, string City, string Country);

public record UpvestTaxResidency(string Country, string TaxIdentifierNumber);

/// <summary>The identity document the shop checked when it verified the shopper.</summary>
public record UpvestKycCheck(
    string DocumentType,
    string DocumentNumber,
    string IssuingCountry,
    DateOnly ExpiryDate,
    UpvestAddress ConfirmedAddress);

public record UpvestUser(string Id, string Status);

public record UpvestAccountGroup(string Id, string Status, string? SecuritiesAccountNumber);

public record UpvestAccount(string Id, string Status);

public record UpvestAllocationEntry(string InstrumentId, decimal Weight);

public record UpvestBankTransaction(
    decimal Amount,
    string Currency,
    string SecuritiesAccountNumber,
    string CounterpartyName);

public record UpvestOrder(string Id, string Status);

public record UpvestRebalancingOrder(string Id, string Status);

public record UpvestValuationPosition(string InstrumentId, decimal Quantity, decimal? Value);

public record UpvestValuation(decimal TotalValue, IReadOnlyList<UpvestValuationPosition> Positions);
