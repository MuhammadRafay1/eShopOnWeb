using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace PublicApiIntegrationTests.Fakes;

/// <summary>
/// A network-free stand-in for PayPal, registered over the real gateway in the test host so the
/// endpoint/ownership/role test suite runs without any PayPal sandbox calls. Simulates realistic,
/// deterministic outcomes: every authorize/capture/refund succeeds; ids are generated locally.
/// </summary>
public class FakePayPalPaymentGateway : IPayPalPaymentGateway
{
    private int _sequence;
    private readonly Dictionary<int, decimal> _authorizedAmountByOrderId = new();

    public Task<AuthorizeResult> AuthorizeOrderAsync(AuthorizeOrderCommand command, CancellationToken cancellationToken)
    {
        var n = Interlocked.Increment(ref _sequence);
        var descriptor = command.PaymentSource.Card is not null
            ? $"card ending {command.PaymentSource.Card.Number[^4..]}"
            : $"saved card {command.PaymentSource.VaultId}";

        _authorizedAmountByOrderId[command.OrderId] = command.Amount;

        return Task.FromResult(new AuthorizeResult(
            $"FAKE-ORDER-{n}", $"FAKE-AUTH-{n}", "CREATED", DateTimeOffset.UtcNow.AddDays(29).ToString("O"), descriptor));
    }

    public Task<CaptureResult> CaptureAsync(CaptureCommand command, CancellationToken cancellationToken)
    {
        var n = Interlocked.Increment(ref _sequence);
        var captureId = $"FAKE-CAPTURE-{n}";
        var gross = _authorizedAmountByOrderId.TryGetValue(command.OrderId, out var amount) ? amount : 0m;
        var fee = Math.Round(gross * 0.03m + 0.30m, 2);
        return Task.FromResult(new CaptureResult(captureId, "COMPLETED", gross, fee, gross - fee));
    }

    public Task<ReauthorizeResult> ReauthorizeAsync(ReauthorizeCommand command, CancellationToken cancellationToken)
    {
        var n = Interlocked.Increment(ref _sequence);
        return Task.FromResult(new ReauthorizeResult($"FAKE-AUTH-{n}", "CREATED", DateTimeOffset.UtcNow.AddDays(29).ToString("O")));
    }

    public Task VoidAsync(string authorizationId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<RefundResult> RefundAsync(RefundCommand command, CancellationToken cancellationToken)
    {
        var n = Interlocked.Increment(ref _sequence);
        return Task.FromResult(new RefundResult($"FAKE-REFUND-{n}", "COMPLETED", command.Amount ?? 0m));
    }

    public Task<VaultCardResult> SaveCardAsync(SaveCardCommand command, CancellationToken cancellationToken)
    {
        var n = Interlocked.Increment(ref _sequence);
        var last4 = command.Card.Number.Length >= 4 ? command.Card.Number[^4..] : command.Card.Number;
        return Task.FromResult(new VaultCardResult(
            $"FAKE-VAULT-{n}", command.ExistingPayPalCustomerId ?? $"FAKE-CUSTOMER-{n}", "VISA", last4,
            command.Card.ExpiryYearMonth, command.Card.CardholderName));
    }

    public Task DeleteCardAsync(string vaultId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<ReconciliationPage> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult(new ReconciliationPage(Array.Empty<ReconciliationTransaction>(), page, 1));
}
