using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Gateway to Upvest, the managed-portfolio provider. All communication with Upvest goes through
/// this abstraction. Methods never throw for the expected "the sandbox client lacks the scope"
/// condition; instead the returned result carries <c>Determined = false</c> so the application can
/// keep the last status it learned from Upvest. Genuine transport/validation failures surface as
/// <see cref="UpvestException"/>.
/// </summary>
public interface IUpvestService
{
    /// <summary>Runs the full investor onboarding sequence (create user, checks, tax residency, account group, account).</summary>
    Task<OnboardingResult> OnboardInvestorAsync(InvestorSignup signup, CancellationToken ct = default);

    /// <summary>Re-reads the investor's acceptance state from Upvest (user + account).</summary>
    Task<InvestorStatusResult> GetInvestorStatusAsync(string upvestUserId, string? upvestAccountId, CancellationToken ct = default);

    /// <summary>Creates (when <paramref name="existingAllocationId"/> is null) or fully replaces an allocation that realises a model.</summary>
    Task<AllocationResult> SaveAllocationAsync(string? existingAllocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct = default);

    /// <summary>Links an account to an allocation (create) or switches it to a different allocation (update).</summary>
    Task<DeterminedResult> ConfigurePortfolioAsync(string accountId, string allocationId, bool alreadyConfigured, CancellationToken ct = default);

    /// <summary>Credits the account group cash balance and places a BUY portfolio order across the model's funds.</summary>
    Task<PayInResult> PayInAsync(string accountGroupId, string upvestUserId, string accountId, decimal amount, string currency, CancellationToken ct = default);

    /// <summary>Re-reads a portfolio order's outcome from Upvest.</summary>
    Task<PaymentStatusResult> GetPaymentStatusAsync(string portfolioOrderId, CancellationToken ct = default);

    /// <summary>Triggers a rebalancing execution realigning every account that follows the given allocation.</summary>
    Task<RealignmentTriggerResult> TriggerRealignmentAsync(string allocationId, CancellationToken ct = default);

    /// <summary>Re-reads a rebalancing execution's outcome from Upvest.</summary>
    Task<RealignmentStatusResult> GetRealignmentStatusAsync(string executionId, CancellationToken ct = default);

    /// <summary>Reads the current portfolio (holdings and valuation) for an account as Upvest records it.</summary>
    Task<PortfolioResult> GetPortfolioAsync(string accountId, CancellationToken ct = default);
}

/// <summary>A fund and its target weight, expressed as a percentage (the model's weights sum to 100).</summary>
public record FundWeight(string InstrumentId, decimal WeightPercent);

/// <summary>The investor sign-up form, as checked by the shop. Treated as personal data; never logged.</summary>
public record InvestorSignup(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    SignupAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocument IdentityDocument);

public record SignupAddress(string Line1, string Postcode, string City, string Country);

public record IdentityDocument(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

/// <summary>Outcome of onboarding. The Upvest user id is always present once the user was created.</summary>
public record OnboardingResult(
    string UpvestUserId,
    string? UpvestAccountGroupId,
    string? UpvestAccountId,
    InvestorStatus Status,
    string? Detail);

public record InvestorStatusResult(bool Determined, InvestorStatus Status, string? Detail);

public record AllocationResult(bool Determined, string? AllocationId, string? Detail);

public record DeterminedResult(bool Determined, string? Detail);

public record PayInResult(bool Determined, string? TopupId, string? PortfolioOrderId, PaymentStatus Status, string? Detail);

public record PaymentStatusResult(bool Determined, PaymentStatus Status, string? Detail);

public record RealignmentTriggerResult(bool Determined, string? ExecutionId, RealignmentStatus Status, string? Detail);

public record RealignmentStatusResult(bool Determined, RealignmentStatus Status, string? Detail);

public record PortfolioHolding(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioResult(
    bool Determined,
    IReadOnlyList<PortfolioHolding> Holdings,
    decimal TotalValue,
    string? Detail);
