using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;

namespace PublicApiIntegrationTests.Portfolio;

/// <summary>
/// Hand-written fake of <see cref="IUpvestGateway"/> for deterministic service tests. Each behaviour is configurable;
/// by default it simulates a ready account so happy-path flows can be exercised without the real provider.
/// Set <see cref="UnauthorizedScopes"/> to make the scoped operations throw the sandbox-style 401, as the real
/// sandbox does for everything beyond user operations.
/// </summary>
public sealed class FakeUpvestGateway : IUpvestGateway
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public Guid AccountGroupId { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; } = Guid.NewGuid();
    public string UserStatus { get; set; } = "ACTIVE";
    public string AccountStatus { get; set; } = "ACTIVE";
    public Guid AllocationId { get; set; } = Guid.NewGuid();
    public string TopupStatus { get; set; } = "CONFIRMED";
    public string OrderStatus { get; set; } = "FILLED";
    public Guid OrderId { get; set; } = Guid.NewGuid();
    public Guid ExecutionId { get; set; } = Guid.NewGuid();
    public string RealignmentStatus { get; set; } = "completed";
    public UpvestValuationResult Valuation { get; set; } =
        new(250.00m, new List<UpvestHolding> { new("US0378331005", 1.5m, 150.00m), new("US5949181045", 1.0m, 100.00m) });
    public ISet<string> TradableIsins { get; set; } = new HashSet<string>();

    /// <summary>Which operations should throw the sandbox unauthorised-scope error. Empty = all succeed.</summary>
    public HashSet<string> UnauthorizedScopes { get; } = new();

    private void Guard(string op)
    {
        if (UnauthorizedScopes.Contains(op))
            throw new UpvestProviderException($"{op} not authorised", HttpStatusCode.Unauthorized, unauthorizedScope: true);
    }

    public Task<UpvestUserResult> CreateUserAsync(InvestorSignupDetails details, Guid idempotencyKey, CancellationToken ct)
    { Guard(nameof(CreateUserAsync)); return Task.FromResult(new UpvestUserResult(UserId, UserStatus)); }

    public Task<Guid> CreateAccountGroupAsync(Guid userId, Guid idempotencyKey, CancellationToken ct)
    { Guard(nameof(CreateAccountGroupAsync)); return Task.FromResult(AccountGroupId); }

    public Task<UpvestAccountResult> CreateAccountAsync(Guid userId, Guid accountGroupId, Guid idempotencyKey, CancellationToken ct)
    { Guard(nameof(CreateAccountAsync)); return Task.FromResult(new UpvestAccountResult(AccountId, AccountGroupId, AccountStatus)); }

    public Task<UpvestUserResult> GetUserAsync(Guid userId, CancellationToken ct)
    { Guard(nameof(GetUserAsync)); return Task.FromResult(new UpvestUserResult(userId, UserStatus)); }

    public Task<string?> GetPortfolioAccountStatusAsync(Guid userId, Guid accountId, CancellationToken ct)
    { Guard(nameof(GetPortfolioAccountStatusAsync)); return Task.FromResult<string?>(AccountStatus); }

    public Task<UpvestAllocationResult> CreateAllocationAsync(string name, IReadOnlyList<FundWeight> funds, Guid idempotencyKey, CancellationToken ct)
    { Guard(nameof(CreateAllocationAsync)); return Task.FromResult(new UpvestAllocationResult(AllocationId)); }

    public Task UpdateAllocationAsync(Guid allocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct)
    { Guard(nameof(UpdateAllocationAsync)); return Task.CompletedTask; }

    public Task CreateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct)
    { Guard(nameof(CreateConfigurationAsync)); return Task.CompletedTask; }

    public Task UpdateConfigurationAsync(Guid accountId, Guid allocationId, Guid idempotencyKey, CancellationToken ct)
    { Guard(nameof(UpdateConfigurationAsync)); return Task.CompletedTask; }

    public Task<Guid?> GetFollowedAllocationAsync(Guid accountId, CancellationToken ct)
    { Guard(nameof(GetFollowedAllocationAsync)); return Task.FromResult<Guid?>(AllocationId); }

    public Task<ISet<string>> ListTradableIsinsAsync(CancellationToken ct)
    { Guard(nameof(ListTradableIsinsAsync)); return Task.FromResult(TradableIsins); }

    public Task<UpvestTopupResult> CreateTopupAsync(Guid accountGroupId, decimal amount, Guid idempotencyKey, CancellationToken ct)
    { Guard(nameof(CreateTopupAsync)); return Task.FromResult(new UpvestTopupResult(Guid.NewGuid(), TopupStatus, "REF-1")); }

    public Task<UpvestOrderResult> CreateBuyOrderAsync(Guid userId, Guid accountId, decimal amount, Guid idempotencyKey, CancellationToken ct)
    { Guard(nameof(CreateBuyOrderAsync)); return Task.FromResult(new UpvestOrderResult(OrderId, OrderStatus)); }

    public Task<string?> GetOrderStatusAsync(Guid orderId, CancellationToken ct)
    { Guard(nameof(GetOrderStatusAsync)); return Task.FromResult<string?>(OrderStatus); }

    public Task<Guid> TriggerRealignmentAsync(Guid allocationId, Guid idempotencyKey, CancellationToken ct)
    { Guard(nameof(TriggerRealignmentAsync)); return Task.FromResult(ExecutionId); }

    public Task<string> GetRealignmentStatusAsync(Guid executionId, CancellationToken ct)
    { Guard(nameof(GetRealignmentStatusAsync)); return Task.FromResult(RealignmentStatus); }

    public Task<UpvestValuationResult> GetValuationAsync(Guid accountId, CancellationToken ct)
    { Guard(nameof(GetValuationAsync)); return Task.FromResult(Valuation); }
}
