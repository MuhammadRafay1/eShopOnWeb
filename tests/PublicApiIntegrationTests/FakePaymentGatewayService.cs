using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace PublicApiIntegrationTests;

/// <summary>
/// Deterministic in-memory stand-in for PayPal, so the endpoint suite proves routing / auth /
/// ownership / state-machine / idempotency behaviour without live network calls. Real PayPal
/// connectivity is proven by the task's separate manual sandbox verification.
/// </summary>
public class FakePaymentGatewayService : IPaymentGatewayService
{
    private int _seq;

    public bool NextAuthorizeRequiresPayerAction { get; set; }
    public List<string> DeletedVaultIds { get; } = new();
    public List<ReconciliationTransaction> Transactions { get; } = new();

    public Task<AuthorizationResult> AuthorizeAsync(AuthorizationRequest request, CancellationToken ct = default)
    {
        if (NextAuthorizeRequiresPayerAction)
            return Task.FromResult(new AuthorizationResult(true, null, null, null, null));

        var n = Interlocked.Increment(ref _seq);
        return Task.FromResult(new AuthorizationResult(false, $"PPO-{n}", $"AUTH-{n}", "CREATED",
            DateTimeOffset.UtcNow.AddDays(29)));
    }

    public Task<AuthorizationSnapshot> GetAuthorizationStatusAsync(string authorizationId, CancellationToken ct = default)
        => Task.FromResult(new AuthorizationSnapshot("CREATED", DateTimeOffset.UtcNow.AddDays(29)));

    public Task<AuthorizationResult> ReauthorizeAsync(string authorizationId, decimal amount, string currency, string orderIdForIdempotency, CancellationToken ct = default)
        => Task.FromResult(new AuthorizationResult(false, null, authorizationId + "-R", "CREATED",
            DateTimeOffset.UtcNow.AddDays(29)));

    public Task VoidAuthorizationAsync(string authorizationId, string orderIdForIdempotency, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<CaptureResult> CaptureAsync(string authorizationId, decimal amount, string currency, string orderIdForIdempotency, CancellationToken ct = default)
    {
        var fee = Math.Round(amount * 0.03m, 2);
        return Task.FromResult(new CaptureResult($"CAP-{authorizationId}", "COMPLETED", amount, fee, amount - fee, currency));
    }

    public Task<RefundResult> RefundAsync(string captureId, decimal? amount, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        var n = Interlocked.Increment(ref _seq);
        return Task.FromResult(new RefundResult($"RF-{n}", "COMPLETED", amount ?? 0m));
    }

    public Task<VaultedCardResult> VaultCardAsync(CardDetails card, CancellationToken ct = default)
    {
        var n = Interlocked.Increment(ref _seq);
        return Task.FromResult(new VaultedCardResult($"VAULT-{n}", "VISA", "1111", "2027-12"));
    }

    public Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct = default)
    {
        DeletedVaultIds.Add(vaultId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReconciliationTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ReconciliationTransaction>>(Transactions);
}
