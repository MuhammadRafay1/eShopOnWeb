using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

public class Buyer : BaseEntity, IAggregateRoot
{
    public string IdentityGuid { get; private set; }
    public string? PayPalCustomerId { get; private set; }

    private readonly List<PaymentMethod> _paymentMethods = new();

    public IReadOnlyCollection<PaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

    #pragma warning disable CS8618 // Required by Entity Framework
    private Buyer() { }

    public Buyer(string identity) : this()
    {
        Guard.Against.NullOrEmpty(identity, nameof(identity));
        IdentityGuid = identity;
    }

    public PaymentMethod AddPaymentMethod(string vaultTokenId, string? last4, string? brand, string? expiry, string? alias)
    {
        var paymentMethod = new PaymentMethod(vaultTokenId, last4, brand, expiry, alias);
        _paymentMethods.Add(paymentMethod);
        return paymentMethod;
    }

    public bool RemovePaymentMethod(int paymentMethodId)
    {
        var paymentMethod = _paymentMethods.FirstOrDefault(p => p.Id == paymentMethodId);
        if (paymentMethod is null)
            return false;

        _paymentMethods.Remove(paymentMethod);
        return true;
    }

    public void SetPayPalCustomerId(string customerId)
    {
        if (string.IsNullOrEmpty(PayPalCustomerId))
            PayPalCustomerId = customerId;
    }
}
