using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace PublicApiIntegrationTests.Payments;

/// <summary>In-memory stand-ins for the PayPal gateways so endpoint/auth/ownership behaviour is
/// testable without live PayPal calls.</summary>
public class FakePaymentGateway : IPayPalPaymentGateway
{
    public Task<PayPalAuthorizationOutcome> AuthorizeAsync(PayPalAuthorizeRequest request, CancellationToken ct)
    {
        return Task.FromResult(new PayPalAuthorizationOutcome
        {
            Approved = true,
            PayPalOrderId = $"PPO-{request.InvoiceId}",
            AuthorizationId = $"AUTH-{request.InvoiceId}-{Guid.NewGuid():N}",
            Status = PaymentAuthorizationStatus.Created,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(29)
        });
    }

    public Task<PayPalCaptureOutcome> CaptureAsync(string authorizationId, decimal amount, string currency,
        string orderIdForInvoice, string idempotencyKey, CancellationToken ct)
    {
        var fee = Math.Round(amount * 0.03m, 2);
        return Task.FromResult(new PayPalCaptureOutcome
        {
            Result = PayPalCaptureResult.Completed,
            CaptureId = $"CAP-{orderIdForInvoice}-{Guid.NewGuid():N}",
            Status = PaymentCaptureStatus.Completed,
            GrossAmount = amount,
            FeeAmount = fee,
            NetAmount = amount - fee
        });
    }

    public Task<PayPalReauthorizeOutcome> ReauthorizeAsync(string authorizationId, decimal amount,
        string currency, CancellationToken ct) =>
        Task.FromResult(new PayPalReauthorizeOutcome
        {
            Result = PayPalReauthorizeResult.Renewed,
            Status = PaymentAuthorizationStatus.Created,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(29)
        });

    public Task VoidAsync(string authorizationId, CancellationToken ct) => Task.CompletedTask;

    public Task<PayPalRefundOutcome> RefundAsync(string captureId, decimal? amount, string currency,
        string idempotencyKey, CancellationToken ct) =>
        Task.FromResult(new PayPalRefundOutcome
        {
            RefundId = $"REF-{Guid.NewGuid():N}",
            Status = "COMPLETED"
        });
}

public class FakeVaultGateway : IPayPalVaultGateway
{
    public List<string> DeletedVaultIds { get; } = new();

    public Task<PayPalSavedCard> SaveCardAsync(string customerId, CardDetails card, CancellationToken ct)
    {
        var last4 = card.Number.Length >= 4 ? card.Number[^4..] : card.Number;
        return Task.FromResult(new PayPalSavedCard
        {
            VaultId = $"VAULT-{Guid.NewGuid():N}",
            Last4 = last4,
            Brand = "VISA",
            ExpiryYearMonth = card.Expiry,
            CardholderName = card.Name
        });
    }

    public Task DeletePaymentTokenAsync(string vaultId, CancellationToken ct)
    {
        DeletedVaultIds.Add(vaultId);
        return Task.CompletedTask;
    }
}

public class FakeReconciliationGateway : IPayPalReconciliationGateway
{
    public Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PayPalTransactionRecord>>(Array.Empty<PayPalTransactionRecord>());
}
