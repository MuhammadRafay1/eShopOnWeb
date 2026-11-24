using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the managed-portfolio flows: it keeps the local correlation records in step with
/// Upvest and derives every reported status from what Upvest actually reports.
/// </summary>
public interface IManagedPortfolioService
{
    Task<Investor> SignUpInvestorAsync(string shopperId, InvestorSignUp signUp, CancellationToken cancellationToken = default);
    Task<Investor?> GetInvestorAsync(string shopperId, CancellationToken cancellationToken = default);

    Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFundInput> funds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelPortfolio>> ListModelsAsync(CancellationToken cancellationToken = default);
    Task<Investor> ChooseModelAsync(string shopperId, string modelId, CancellationToken cancellationToken = default);

    Task<PortfolioPayment> PayInAsync(string shopperId, decimal amount, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortfolioPayment>> ListPaymentsAsync(string shopperId, CancellationToken cancellationToken = default);

    Task<Realignment> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken = default);
    Task<Realignment?> GetRealignmentAsync(string realignmentId, CancellationToken cancellationToken = default);

    Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken cancellationToken = default);
}

/// <summary>The shop's investor sign-up form plus the identity document the shop checked.</summary>
public record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    AddressInput Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    IdentityDocumentInput IdentityDocument);

public record AddressInput(string Line1, string Postcode, string City, string Country);

public record IdentityDocumentInput(string Type, string Number, string IssuingCountry, DateOnly ExpiryDate);

public record ModelFundInput(string InstrumentId, decimal Weight);

public record PortfolioHoldingView(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioView(
    string? ModelId,
    IReadOnlyList<PortfolioHoldingView> Holdings,
    decimal TotalValue,
    decimal PaidIn);
