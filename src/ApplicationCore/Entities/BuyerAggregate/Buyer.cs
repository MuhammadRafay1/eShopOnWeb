using System.Collections.Generic;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

public class Buyer : BaseEntity, IAggregateRoot
{
    public string IdentityGuid { get; private set; }

    private List<PaymentMethod> _paymentMethods = new List<PaymentMethod>();

    public IReadOnlyCollection<PaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

    #pragma warning disable CS8618 // Required by Entity Framework
    private Buyer() { }

    public Buyer(string identity) : this()
    {
        Guard.Against.NullOrEmpty(identity, nameof(identity));
        IdentityGuid = identity;
    }

    public PaymentMethod AddPaymentMethod(string payPalVaultId, string? brand, string? last4, string? expiry)
    {
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));
        var paymentMethod = new PaymentMethod(Id, payPalVaultId, brand, last4, expiry);
        _paymentMethods.Add(paymentMethod);
        return paymentMethod;
    }

    public bool RemovePaymentMethod(int paymentMethodId)
    {
        var existing = _paymentMethods.Find(p => p.Id == paymentMethodId);
        if (existing is null)
        {
            return false;
        }

        _paymentMethods.Remove(existing);
        return true;
    }
}
