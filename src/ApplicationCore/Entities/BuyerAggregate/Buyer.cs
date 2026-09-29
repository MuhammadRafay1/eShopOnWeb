using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

public class Buyer : BaseEntity, IAggregateRoot
{
    /// <summary>
    /// The shopper's username — the same identity string used everywhere else in this codebase
    /// as Order.BuyerId / Basket.BuyerId (i.e. User.Identity.Name).
    /// </summary>
    public string IdentityGuid { get; private set; }

    private List<PaymentMethod> _paymentMethods = new List<PaymentMethod>();

    public IEnumerable<PaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

    #pragma warning disable CS8618 // Required by Entity Framework
    private Buyer() { }

    public Buyer(string identity) : this()
    {
        Guard.Against.NullOrEmpty(identity, nameof(identity));
        IdentityGuid = identity;
    }

    public PaymentMethod AddPaymentMethod(PaymentMethod paymentMethod)
    {
        Guard.Against.Null(paymentMethod, nameof(paymentMethod));
        _paymentMethods.Add(paymentMethod);
        return paymentMethod;
    }

    /// <summary>
    /// Removes a saved card by its local id. Returns the removed method, or null if the buyer
    /// has no such card (so the caller can treat "not found / not owned" uniformly).
    /// </summary>
    public PaymentMethod? RemovePaymentMethod(int paymentMethodId)
    {
        var method = _paymentMethods.FirstOrDefault(pm => pm.Id == paymentMethodId);
        if (method is not null)
        {
            _paymentMethods.Remove(method);
        }
        return method;
    }
}
