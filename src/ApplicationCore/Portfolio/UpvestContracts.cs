using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

// Lightweight projections of Upvest API resources — only the fields this integration needs.

public sealed record UpvestUser(string Id, string Status);

public sealed record UpvestAccountGroup(string Id, string Status);

public sealed record UpvestAccount(string Id, string Status);

public sealed record UpvestFund(string InstrumentId, string InstrumentIdType, string Weight);

public sealed record UpvestAllocation(string Id, string Name, IReadOnlyList<UpvestFund> Funds);

public sealed record UpvestConfiguration(string AccountId, string AllocationId);

public sealed record UpvestPortfolioOrder(string Id, string Status);

public sealed record UpvestRebalancingOrder(string Id, string Status, string? CancellationReason);

public sealed record UpvestPosition(string InstrumentId, decimal Quantity, decimal Value);

public sealed record UpvestValuation(decimal TotalValue, string Currency, IReadOnlyList<UpvestPosition> Positions);

/// <summary>Inputs for KYC check submission, derived from the shop's verification.</summary>
public sealed record KycCheckInput(
    string DocumentType,
    DateOnly? DocumentExpirationDate,
    string Nationality,
    PostalAddress ConfirmedAddress);

/// <summary>A tax residency declaration.</summary>
public sealed record TaxResidencyInput(string Country, string? TaxIdentifierNumber);

/// <summary>
/// Raised when Upvest returns a non-success response. Carries the HTTP status and the
/// Upvest error <c>type</c> so callers can react (e.g. treat 401 scope errors as "not yet known").
/// Never carries request personal data.
/// </summary>
public sealed class UpvestApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorType { get; }

    public UpvestApiException(int statusCode, string? errorType, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
    }

    /// <summary>True when the failure is Upvest refusing the requested scopes for this client.</summary>
    public bool IsNotAuthorisedScopes =>
        StatusCode == 401 &&
        (Message?.Contains("not authorised to use these scopes", StringComparison.OrdinalIgnoreCase) ?? false);
}
