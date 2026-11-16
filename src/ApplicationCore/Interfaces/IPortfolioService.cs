using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the managed-portfolio flows: it owns the application's persistence and claims, calls
/// <see cref="IUpvestPortfolioGateway"/> for every Upvest interaction, and keeps each shopper's records private
/// to that shopper. Status is always what the gateway reads back from Upvest.
/// </summary>
public interface IPortfolioService
{
    // Flow 1
    Task<Investor> RegisterInvestorAsync(string shopperUserName, InvestorRegistration registration, CancellationToken cancellationToken);
    Task<Investor?> GetInvestorAsync(string shopperUserName, CancellationToken cancellationToken);

    // Flow 2
    Task<ModelPortfolio> UpsertModelAsync(string modelId, string name, IReadOnlyList<FundWeight> funds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelPortfolio>> ListModelsAsync(CancellationToken cancellationToken);
    Task<Investor> ChooseModelAsync(string shopperUserName, string modelId, CancellationToken cancellationToken);

    // Flow 3
    Task<PortfolioPayment> PayInAsync(string shopperUserName, decimal amount, Guid? idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<PortfolioPayment>> GetPaymentsAsync(string shopperUserName, CancellationToken cancellationToken);

    // Flow 4
    Task<Realignment> TriggerRealignmentAsync(string modelId, CancellationToken cancellationToken);
    Task<RealignmentStatus> GetRealignmentStatusAsync(Guid realignmentId, CancellationToken cancellationToken);

    // Flow 5
    Task<PortfolioView> GetPortfolioAsync(string shopperUserName, CancellationToken cancellationToken);
}

/// <summary>Thrown by the portfolio service for caller-side rule violations that need a specific HTTP status.</summary>
public class PortfolioRequestException : Exception
{
    public PortfolioRequestException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>The HTTP status the API boundary should return (e.g. 404, 409, 422).</summary>
    public int StatusCode { get; }
}
