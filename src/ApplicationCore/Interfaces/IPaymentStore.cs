using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Persistence for payment state, with the two guarantees the payment flows rely on:
/// a claim insert is refused by the store when the key already exists, and an order save is refused when
/// another request changed the order since it was loaded (<see cref="Exceptions.PaymentConflictException"/>).
/// </summary>
public interface IPaymentStore
{
    /// <summary>Inserts the claim; returns false when a claim with the same key already exists.</summary>
    Task<bool> TryAddClaimAsync(PaymentOperationClaim claim, CancellationToken cancellationToken);

    Task<PaymentOperationClaim?> GetClaimAsync(string key, CancellationToken cancellationToken);

    /// <summary>Saves a claim state change; returns false when another request changed it first.</summary>
    Task<bool> TrySaveClaimAsync(PaymentOperationClaim claim, CancellationToken cancellationToken);

    /// <summary>Deletes the claim so the operation can be attempted again (used when the provider refused it).</summary>
    Task ReleaseClaimAsync(PaymentOperationClaim claim, CancellationToken cancellationToken);

    Task<Order?> GetOrderAsync(int orderId, CancellationToken cancellationToken);

    /// <summary>Persists order/payment changes; throws <see cref="Exceptions.PaymentConflictException"/> on a lost race.</summary>
    Task SaveOrderAsync(Order order, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> ListOrdersWithPaymentActivityAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken);

    Task<SavedPaymentMethod?> GetSavedPaymentMethodAsync(int id, string buyerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SavedPaymentMethod>> ListSavedPaymentMethodsAsync(string buyerId, bool includeRemoved,
        CancellationToken cancellationToken);

    Task AddSavedPaymentMethodAsync(SavedPaymentMethod method, CancellationToken cancellationToken);

    Task SaveSavedPaymentMethodAsync(SavedPaymentMethod method, CancellationToken cancellationToken);
}
