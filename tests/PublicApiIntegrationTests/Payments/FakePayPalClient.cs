using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace PublicApiIntegrationTests.Payments;

/// <summary>
/// A hermetic stand-in for the real PayPal client. Keeps the integration suite free of network
/// calls (credentials are per-run env vars, not a CI fixture) while behaving faithfully enough to
/// exercise the auth/role/ownership/idempotency/state logic: it tracks authorized amounts so
/// capture reports a real captured amount, fee, and net.
/// </summary>
public class FakePayPalClient : IPayPalClient
{
    private int _counter;
    private readonly ConcurrentDictionary<string, decimal> _amountByAuthId = new();
    private readonly ConcurrentDictionary<string, decimal> _amountByCaptureId = new();

    private string Next(string prefix) => $"{prefix}-{Interlocked.Increment(ref _counter)}";

    public Task<AuthorizationResult> AuthorizeWithCardAsync(int orderId, string currencyCode, decimal amount,
        CardDetails card, CancellationToken cancellationToken = default) => Authorize(amount);

    public Task<AuthorizationResult> AuthorizeWithVaultedCardAsync(int orderId, string currencyCode, decimal amount,
        string vaultId, CancellationToken cancellationToken = default) => Authorize(amount);

    private Task<AuthorizationResult> Authorize(decimal amount)
    {
        var authId = Next("AUTH");
        _amountByAuthId[authId] = amount;
        return Task.FromResult(new AuthorizationResult(Next("PPORDER"), authId, "CREATED",
            DateTimeOffset.UtcNow.AddDays(3)));
    }

    public Task<AuthorizationDetails> GetAuthorizationAsync(string authorizationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new AuthorizationDetails(authorizationId, "CREATED",
            DateTimeOffset.UtcNow.AddDays(3), null));

    public Task<ReauthorizationResult> ReauthorizeAsync(string authorizationId, string currencyCode, decimal amount,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var authId = Next("AUTH");
        _amountByAuthId[authId] = amount;
        return Task.FromResult(new ReauthorizationResult(authId, "CREATED", DateTimeOffset.UtcNow.AddDays(3)));
    }

    public Task<CaptureResult> CaptureAsync(string authorizationId, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var amount = _amountByAuthId.TryGetValue(authorizationId, out var a) ? a : 0m;
        var captureId = Next("CAP");
        _amountByCaptureId[captureId] = amount;
        var fee = Math.Round(amount * 0.03m, 2);
        return Task.FromResult(new CaptureResult(captureId, "COMPLETED", amount, fee, amount - fee));
    }

    public Task VoidAsync(string authorizationId, string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<RefundResult> RefundAsync(string captureId, string currencyCode, decimal? amount,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var refundAmount = amount ?? (_amountByCaptureId.TryGetValue(captureId, out var a) ? a : 0m);
        return Task.FromResult(new RefundResult(Next("REF"), "COMPLETED", refundAmount));
    }

    public Task<SetupTokenResult> CreateSetupTokenAsync(CardDetails card, CancellationToken cancellationToken = default)
        => Task.FromResult(new SetupTokenResult(Next("SETUP"), "APPROVED"));

    public Task<VaultedCard> CreatePaymentTokenAsync(string setupTokenId, CancellationToken cancellationToken = default)
        => Task.FromResult(new VaultedCard(Next("VAULT"), "VISA", "1111", "2030-01"));

    public Task DeletePaymentTokenAsync(string vaultId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<PayPalTransaction>> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PayPalTransaction>>(new List<PayPalTransaction>());
}
