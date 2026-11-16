using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.PublicApi.Portfolio;

/// <summary>
/// The application's boundary to Upvest. Each method speaks in app-level types and translates every
/// SDK outcome (success, typed error, transport failure, unknown write outcome) into either a result
/// or a <see cref="UpvestGatewayException"/>. Nothing above this interface references the Upvest SDK.
/// Idempotency keys are supplied by the caller and persisted alongside the app record, so a retry
/// reuses the same key and never creates a duplicate at Upvest.
/// </summary>
public interface IUpvestInvestmentGateway
{
    // Flow 1 — onboarding
    Task<Guid> CreateUserAsync(InvestorRegistration reg, Guid idempotencyKey, CancellationToken ct);
    Task<Guid> CreateKycCheckAsync(Guid userId, InvestorRegistration reg, CancellationToken ct);
    Task SetTaxResidencyAsync(Guid userId, string taxId, string taxCountry, Guid idempotencyKey, CancellationToken ct);
    Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken ct);
    Task<Guid> CreateAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken ct);
    Task<InvestorState> GetInvestorStateAsync(Guid userId, Guid? kycCheckId, Guid? accountGroupId, CancellationToken ct);

    // Flow 2 — models
    Task<Guid> CreateAllocationAsync(string name, IReadOnlyList<UpvestFund> funds, Guid idempotencyKey, CancellationToken ct);
    Task UpdateAllocationAsync(Guid allocationId, string name, IReadOnlyList<UpvestFund> funds, CancellationToken ct);
    Task<IReadOnlyList<UpvestAllocation>> ListAllocationsAsync(CancellationToken ct);
    Task SetConfigurationAsync(Guid accountId, Guid allocationId, bool isSwitch, Guid idempotencyKey, CancellationToken ct);

    // Flow 3 — pay in
    Task<TopupResult> CreateTopupAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken ct);
    Task SimulateInboundTransferAsync(decimal amount, string clientReference, string? settlementReference, Guid idempotencyKey, CancellationToken ct);
    Task<Guid> PlaceBuyOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken ct);
    Task<PaymentState> GetPaymentStateAsync(Guid? topupId, Guid? orderId, CancellationToken ct);

    // Flow 4 — realignment
    Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct);
    Task<RealignmentState> GetRealignmentStateAsync(Guid executionId, CancellationToken ct);

    // Flow 5 — portfolio
    Task<UpvestPortfolio> GetPortfolioAsync(Guid accountId, CancellationToken ct);
}
