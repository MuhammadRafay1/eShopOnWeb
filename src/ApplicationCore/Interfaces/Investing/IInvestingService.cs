using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;

/// <summary>
/// The capabilities the PublicApi investing endpoints drive. One shopper's enrolment, ledger and
/// investments are private to that shopper; every method is scoped by <c>shopperId</c>.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt the shopper in. Idempotent per shopper — returns the existing enrolment if already enrolled.</summary>
    Task<Enrolment> EnrolAsync(string shopperId, InvestorSignUpDetails details, CancellationToken cancellationToken);

    Task<Enrolment?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken);

    /// <summary>
    /// Place a shop order from catalog items for the shopper and set aside the round-up if the shopper is an
    /// accepted investor. Never throws for anything investing-related. Returns the new order id and the
    /// amount set aside (0 when nothing was set aside).
    /// </summary>
    Task<PlaceOrderResult> PlaceOrderAsync(string shopperId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken);

    Task<BalanceView> GetBalanceAsync(string shopperId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string shopperId, CancellationToken cancellationToken);
}

/// <summary>A requested order line: a catalog item and a quantity.</summary>
public readonly record struct OrderLine(int CatalogItemId, int Quantity);

/// <summary>Outcome of placing a shop order.</summary>
public readonly record struct PlaceOrderResult(int OrderId, decimal RoundUpAmount);

/// <summary>The shopper's balance: set aside but not yet invested, and the total invested so far.</summary>
public readonly record struct BalanceView(decimal PendingAmount, decimal InvestedAmount);
