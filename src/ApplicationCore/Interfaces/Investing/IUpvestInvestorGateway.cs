using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;

/// <summary>
/// Abstraction over everything the application needs from the investment provider (Upvest). Keeps the
/// provider SDK out of the domain and services. Implementations translate provider failures into a single
/// failure type and never leak personal data into logs.
/// </summary>
public interface IUpvestInvestorGateway
{
    /// <summary>
    /// Provisions the shopper as an investor at the provider (investor + personal account group + trading
    /// account). <paramref name="buyerId"/> seeds deterministic idempotency keys so a retry does not create
    /// duplicates. Returns the provider ids and the acceptance status at the time of provisioning.
    /// </summary>
    Task<InvestorProvisionResult> ProvisionInvestorAsync(
        string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken);

    /// <summary>Re-reads the shopper's acceptance status from the provider (for reconciliation).</summary>
    Task<EnrolmentStatus> GetEnrolmentStatusAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Places a buy order investing <paramref name="amountCents"/> into the configured fund for the
    /// shopper's account. <paramref name="reference"/> is the order's client reference and idempotency key.
    /// </summary>
    Task<InvestmentOrderResult> PlaceInvestmentOrderAsync(
        Guid userId, Guid accountId, long amountCents, Guid reference, CancellationToken cancellationToken);

    /// <summary>Reads an order's current investment status from the provider (by order id).</summary>
    Task<InvestmentStatus> GetOrderStatusAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Finds an order the application placed, by the client reference it sent, when the order id is unknown
    /// (e.g. the placement connection failed). Returns null if no such order exists at the provider.
    /// </summary>
    Task<InvestmentOrderResult?> FindOrderByReferenceAsync(
        Guid accountId, Guid reference, CancellationToken cancellationToken);
}

/// <summary>The shop's investor sign-up details, mapped to the provider's onboarding payload.</summary>
public sealed record InvestorEnrolmentDetails(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string Nationality,
    string AddressLine1,
    string Postcode,
    string City,
    string Country,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry);

/// <summary>Provider ids and acceptance status after provisioning an investor.</summary>
public sealed record InvestorProvisionResult(
    Guid UpvestUserId, Guid AccountGroupId, Guid AccountId, EnrolmentStatus Status);

/// <summary>Outcome of placing (or re-reading) an investment order at the provider.</summary>
public sealed record InvestmentOrderResult(Guid OrderId, InvestmentStatus Status);
