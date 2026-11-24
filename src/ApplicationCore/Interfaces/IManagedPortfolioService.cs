using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

// Input contracts (carry data supplied by callers; personal data flows straight to the provider).

public record InvestorIdentityDocument(string Type, string Number, string IssuingCountry, string ExpiryDate);

public record InvestorAddress(string Line1, string Postcode, string City, string Country);

public record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string BirthCity,
    string BirthCountry,
    string Nationality,
    InvestorAddress Address,
    string PhoneNumber,
    string TaxId,
    string TaxCountry,
    InvestorIdentityDocument IdentityDocument);

public record FundInput(string InstrumentId, decimal Weight);

// Result contracts (shaped by endpoints into their JSON responses).

public record InvestorResult(string InvestorId, InvestorStatus Status);

public record FundResult(string InstrumentId, decimal Weight);

public record ModelResult(string ModelId, string Name, IReadOnlyList<FundResult> Funds);

public record PaymentResult(Guid PaymentId, decimal Amount, PaymentStatus Status);

public record RealignmentResult(Guid RealignmentId, RealignmentStatus Status);

public record HoldingResult(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioResult(string? ModelId, IReadOnlyList<HoldingResult> Holdings, decimal TotalValue, decimal PaidIn);

/// <summary>
/// Orchestrates the managed-portfolio capability: investor onboarding, model portfolios,
/// pay-ins, realignments and portfolio views, coordinating local bookkeeping with Upvest.
/// Every status it reports reflects the live state at Upvest.
/// </summary>
public interface IManagedPortfolioService
{
    Task<InvestorResult> SignUpInvestorAsync(string shopperUserName, InvestorSignUp signUp, CancellationToken ct = default);
    Task<InvestorResult?> GetInvestorAsync(string shopperUserName, CancellationToken ct = default);

    Task<ModelResult> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundInput> funds, CancellationToken ct = default);
    Task<IReadOnlyList<ModelResult>> GetModelsAsync(CancellationToken ct = default);
    Task SelectModelAsync(string shopperUserName, string modelId, CancellationToken ct = default);

    Task<PaymentResult> PayInAsync(string shopperUserName, decimal amount, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentResult>> GetPaymentsAsync(string shopperUserName, CancellationToken ct = default);

    Task<RealignmentResult> TriggerRealignmentAsync(string modelId, CancellationToken ct = default);
    Task<RealignmentResult> GetRealignmentAsync(Guid realignmentId, CancellationToken ct = default);

    Task<PortfolioResult?> GetPortfolioAsync(string shopperUserName, CancellationToken ct = default);
}
