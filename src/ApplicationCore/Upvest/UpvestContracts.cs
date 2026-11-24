using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Upvest;

// ---------------------------------------------------------------------------
// Domain-facing contract types for the Upvest gateway. These intentionally
// expose only the fields this integration needs; they carry no secrets.
// ---------------------------------------------------------------------------

/// <summary>Address as the Upvest user/KYC payloads expect it.</summary>
public record UpvestAddress(
    string Line1,
    string Postcode,
    string City,
    string Country);

/// <summary>Everything needed to create an Upvest user from the shop's sign-up form.</summary>
public record UpvestCreateUserRequest(
    string FirstName,
    string LastName,
    string? Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    UpvestAddress Address,
    string? PhoneNumber);

/// <summary>Identity-document details to register a KYC check against a user.</summary>
public record UpvestKycCheckRequest(
    string DocumentType,
    string IssuingCountry,
    DateOnly ExpiryDate,
    UpvestAddress Address);

/// <summary>Upvest user status: <c>ACTIVE</c>, <c>INACTIVE</c>, <c>OFFBOARDING</c>, <c>OFFBOARDED</c>.</summary>
public record UpvestUser(string Id, string Status);

/// <summary>Upvest check status: <c>IN_PROGRESS</c>, <c>PASSED</c>, <c>FAILED</c>.</summary>
public record UpvestCheck(string Id, string Status);

/// <summary>Account-group status mirrors the account status enum.</summary>
public record UpvestAccountGroup(string Id, string Status);

/// <summary>Account status: <c>PENDING_APPROVAL</c>, <c>ACTIVE</c>, <c>CLOSING</c>, <c>CLOSED</c>, <c>LOCKED</c>.</summary>
public record UpvestAccount(string Id, string AccountGroupId, string Status);

/// <summary>One instrument weight within an allocation; <paramref name="Isin"/> is the fund's ISIN.</summary>
public record UpvestAllocationEntry(string Isin, decimal Weight);

public record UpvestAllocation(string Id, string Name, IReadOnlyList<UpvestAllocationEntry> Funds);

/// <summary>Portfolio order status: <c>NEW</c>, <c>PROCESSING</c>, <c>FILLED</c>, <c>SETTLED</c>, <c>CANCELLED</c>.</summary>
public record UpvestOrder(string Id, string Status, string? CancellationReason = null);

/// <summary>A rebalancing execution. Upvest exposes no top-level status; it is derived from its orders.</summary>
public record UpvestRebalancing(string Id, IReadOnlyList<UpvestOrder> Orders);

public record UpvestHolding(string InstrumentId, decimal Quantity, decimal Value);

public record UpvestValuation(
    IReadOnlyList<UpvestHolding> Holdings,
    decimal TotalValue,
    string Currency);
