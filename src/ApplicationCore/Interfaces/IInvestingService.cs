using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the "invest your change" capability: enrolment, setting aside the round-up
/// on each paid order, investing once the balance crosses the threshold, and reconciling
/// state against Upvest. All methods are scoped to a single shopper (<c>buyerId</c>) except
/// the reconcile sweep.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt the shopper in. Idempotent: returns the existing enrolment if present.</summary>
    Task<EnrolmentView> EnrolAsync(string buyerId, InvestorSignUp signUp, CancellationToken cancellationToken = default);

    /// <summary>The shopper's enrolment, or null if they have never enrolled.</summary>
    Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A paid order: set aside its round-up for an accepted investor and invest if the
    /// balance has reached the threshold. Returns the amount this order set aside (0 if
    /// none). Never throws for investing reasons.
    /// </summary>
    Task<decimal> HandlePaidOrderAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken cancellationToken = default);

    Task<BalanceView> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>The shopper's investments, newest first.</summary>
    Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconcile every pending enrolment and investment against Upvest's current state.
    /// Driven by the background poller and by incoming Upvest webhooks.
    /// </summary>
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}

/// <summary>The shop's investor sign-up form.</summary>
public record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    SignUpAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry);

public record SignUpAddress(string Line1, string Postcode, string City, string Country);

/// <summary>enrolmentId + status ("pending" | "active" | "rejected").</summary>
public record EnrolmentView(int EnrolmentId, string Status);

/// <summary>pendingAmount + investedAmount, in euros.</summary>
public record BalanceView(decimal PendingAmount, decimal InvestedAmount);

/// <summary>investmentId + amount (euros) + status ("pending" | "settled" | "failed").</summary>
public record InvestmentView(int InvestmentId, decimal Amount, string Status);
