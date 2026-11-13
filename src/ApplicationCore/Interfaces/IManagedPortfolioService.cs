using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the managed-investment-portfolio capability on top of Upvest. All methods act
/// for a single shopper (identified by <c>shopperId</c>, the caller's token identity); one
/// shopper can never see or affect another's investor record, payments or portfolio.
/// Every reported status reflects what actually happened at Upvest.
/// </summary>
public interface IManagedPortfolioService
{
    // Flow 1 — becoming an investor
    Task<InvestorView> SignUpInvestorAsync(string shopperId, InvestorSignUp signUp, CancellationToken ct);
    Task<InvestorView?> GetInvestorAsync(string shopperId, CancellationToken ct);

    // Flow 2 — model portfolios
    Task<ModelView> PutModelAsync(int modelId, ModelDefinition definition, CancellationToken ct);
    Task<IReadOnlyList<ModelView>> ListModelsAsync(CancellationToken ct);
    Task<PortfolioOperationResult<ChosenModelView>> ChooseModelAsync(string shopperId, int modelId, CancellationToken ct);

    // Flow 3 — paying in
    Task<PortfolioOperationResult<PaymentView>> CreatePaymentAsync(string shopperId, decimal amount, CancellationToken ct);
    Task<IReadOnlyList<PaymentView>> ListPaymentsAsync(string shopperId, CancellationToken ct);

    // Flow 4 — realignment
    Task<PortfolioOperationResult<RealignmentView>> CreateRealignmentAsync(int modelId, CancellationToken ct);
    Task<RealignmentView?> GetRealignmentAsync(int realignmentId, CancellationToken ct);

    // Flow 5 — the investor's portfolio
    Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken ct);
}

// ---- Service inputs ----

public record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    string BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    AddressInput Address,
    string? PhoneNumber,
    string? TaxId,
    string? TaxCountry,
    IdentityDocumentInput IdentityDocument);

public record AddressInput(string Line1, string Postcode, string City, string Country);

public record IdentityDocumentInput(string Type, string Number, string IssuingCountry, string ExpiryDate);

public record ModelDefinition(string Name, IReadOnlyList<FundInput> Funds);

public record FundInput(string InstrumentId, decimal Weight);

// ---- Service outputs (field names chosen to match the API contract) ----

public record InvestorView(string InvestorId, string Status);

public record ChosenModelView(string InvestorId, string Status, int ModelId);

public record FundView(string InstrumentId, decimal Weight);

public record ModelView(int ModelId, string Name, IReadOnlyList<FundView> Funds);

public record PaymentView(string PaymentId, decimal Amount, string Status);

public record RealignmentView(string RealignmentId, string Status);

public record HoldingView(string InstrumentId, decimal Quantity, decimal Value);

public record PortfolioView(int? ModelId, IReadOnlyList<HoldingView> Holdings, decimal TotalValue, decimal PaidIn);

/// <summary>Outcome of an operation that can be refused for a business reason.</summary>
public enum PortfolioOperationStatus
{
    Ok,
    NotFound,
    NotEligible,
    Invalid
}

public class PortfolioOperationResult<T>
{
    public PortfolioOperationStatus Status { get; init; }
    public string? Message { get; init; }
    public T? Value { get; init; }

    public static PortfolioOperationResult<T> Ok(T value) => new() { Status = PortfolioOperationStatus.Ok, Value = value };
    public static PortfolioOperationResult<T> NotFound(string message) => new() { Status = PortfolioOperationStatus.NotFound, Message = message };
    public static PortfolioOperationResult<T> NotEligible(string message) => new() { Status = PortfolioOperationStatus.NotEligible, Message = message };
    public static PortfolioOperationResult<T> Invalid(string message) => new() { Status = PortfolioOperationStatus.Invalid, Message = message };
}
