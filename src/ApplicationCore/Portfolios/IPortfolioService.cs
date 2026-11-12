using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolios;

/// <summary>The shop's investor sign-up form.</summary>
public record InvestorSignUp(
    string FirstName,
    string LastName,
    string Email,
    DateOnly BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    UpvestAddress Address,
    string? PhoneNumber,
    string TaxId,
    string TaxCountry,
    UpvestIdentityDocument IdentityDocument);

/// <summary>A fund the operator put in a model, as supplied on the model PUT.</summary>
public record ModelFundInput(string InstrumentId, decimal Weight);

/// <summary>The caller's portfolio as Upvest currently records it.</summary>
public record PortfolioView(
    string? ModelId,
    IReadOnlyList<UpvestHolding> Holdings,
    decimal TotalValue,
    decimal PaidIn);

/// <summary>
/// Orchestrates the managed-portfolio flows across the local stores and Upvest. All statuses it
/// reports are refreshed from Upvest through Upvest's own read endpoints.
/// </summary>
public interface IPortfolioService
{
    Task<Investor> SignUpInvestorAsync(string shopperId, InvestorSignUp form, CancellationToken ct = default);
    Task<Investor?> GetInvestorAsync(string shopperId, CancellationToken ct = default);

    Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<ModelFundInput> funds, CancellationToken ct = default);
    Task<IReadOnlyList<ModelPortfolio>> ListModelsAsync(CancellationToken ct = default);
    Task<Investor> ChooseModelAsync(string shopperId, string modelId, CancellationToken ct = default);

    Task<Payment> PayInAsync(string shopperId, decimal amount, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> ListPaymentsAsync(string shopperId, CancellationToken ct = default);

    Task<Realignment> CreateRealignmentAsync(string modelId, CancellationToken ct = default);
    Task<Realignment?> GetRealignmentAsync(Guid realignmentId, CancellationToken ct = default);

    Task<PortfolioView?> GetPortfolioAsync(string shopperId, CancellationToken ct = default);
}

/// <summary>A caller error (bad input or a disallowed state transition). Maps to 4xx.</summary>
public class PortfolioValidationException : Exception
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>A conflicting state, e.g. signing up twice. Maps to 409.</summary>
public class PortfolioConflictException : Exception
{
    public PortfolioConflictException(string message) : base(message) { }
}

/// <summary>The caller is not (yet) an investor and tried an investor-only action. Maps to 404/409.</summary>
public class NotAnInvestorException : Exception
{
    public NotAnInvestorException(string message) : base(message) { }
}
