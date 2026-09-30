using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;

namespace PublicApiIntegrationTests.Fakes;

/// <summary>
/// In-memory fake of the PayPal gateway so endpoint integration tests are deterministic and network-free.
/// Mirrors the real amounts (authorized -> captured -> refunded) so response fields are meaningful.
/// </summary>
public class FakePayPalGateway : IPayPalPaymentGateway
{
    private int _counter;
    private readonly ConcurrentDictionary<string, decimal> _authAmounts = new();
    private readonly ConcurrentDictionary<string, decimal> _captureAmounts = new();

    private string NextId(string prefix) => $"{prefix}-{Interlocked.Increment(ref _counter)}";

    public Task<AuthorizationResult> AuthorizeWithCardAsync(string idempotencyKey, int orderId, decimal amount, string currency, CardDetails card, CancellationToken ct)
        => Authorize(amount);

    public Task<AuthorizationResult> AuthorizeWithVaultedCardAsync(string idempotencyKey, int orderId, decimal amount, string currency, string vaultId, CancellationToken ct)
        => Authorize(amount);

    private Task<AuthorizationResult> Authorize(decimal amount)
    {
        var authId = NextId("AUTH");
        _authAmounts[authId] = amount;
        return Task.FromResult(new AuthorizationResult("PPO-" + authId, authId, "COMPLETED", amount, DateTimeOffset.UtcNow.AddDays(29), false));
    }

    public Task<AuthorizationResult> ReauthorizeAsync(string idempotencyKey, string authorizationId, decimal amount, string currency, CancellationToken ct)
    {
        var authId = NextId("AUTH");
        _authAmounts[authId] = amount;
        return Task.FromResult(new AuthorizationResult("PPO-" + authId, authId, "CREATED", amount, DateTimeOffset.UtcNow.AddDays(29), false));
    }

    public Task<AuthorizationResult> GetAuthorizationAsync(string authorizationId, CancellationToken ct)
        => Task.FromResult(new AuthorizationResult("PPO", authorizationId, "CREATED", _authAmounts.GetValueOrDefault(authorizationId), DateTimeOffset.UtcNow.AddDays(29), false));

    public Task VoidAuthorizationAsync(string idempotencyKey, string authorizationId, CancellationToken ct) => Task.CompletedTask;

    public Task<CaptureResult> CaptureAuthorizationAsync(string idempotencyKey, string authorizationId, CancellationToken ct)
    {
        var amount = _authAmounts.GetValueOrDefault(authorizationId, 0m);
        var capId = NextId("CAP");
        _captureAmounts[capId] = amount;
        var fee = decimal.Round(amount * 0.03m, 2);
        return Task.FromResult(new CaptureResult(capId, "COMPLETED", amount, fee, amount - fee));
    }

    public Task<CaptureResult> GetCaptureAsync(string captureId, CancellationToken ct)
    {
        var amount = _captureAmounts.GetValueOrDefault(captureId, 0m);
        var fee = decimal.Round(amount * 0.03m, 2);
        return Task.FromResult(new CaptureResult(captureId, "COMPLETED", amount, fee, amount - fee));
    }

    public Task<RefundResult> RefundCaptureAsync(string idempotencyKey, string captureId, decimal? amount, string currency, CancellationToken ct)
    {
        var value = amount ?? _captureAmounts.GetValueOrDefault(captureId, 0m);
        return Task.FromResult(new RefundResult(NextId("REF"), "COMPLETED", value, value));
    }

    public Task<RefundResult> GetRefundAsync(string refundId, CancellationToken ct)
        => Task.FromResult(new RefundResult(refundId, "COMPLETED", 0m, 0m));

    public Task<VaultedCardResult> CreateVaultedCardAsync(string idempotencyKey, string buyerId, CardDetails card, CancellationToken ct)
        => Task.FromResult(new VaultedCardResult(NextId("VAULT"), "VISA", "1111", "2030-12"));

    public Task DeleteVaultedCardAsync(string vaultId, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PayPalTransactionRecord>>(new List<PayPalTransactionRecord>());
}
