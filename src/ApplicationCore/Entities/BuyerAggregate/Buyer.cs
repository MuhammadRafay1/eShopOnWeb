using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

public class Buyer : BaseEntity, IAggregateRoot
{
    /// <summary>
    /// The shopper's identity - kept keyed by username/email exactly like Order.BuyerId and
    /// Basket.BuyerId, so it can be looked up from User.Identity!.Name without touching the JWT.
    /// </summary>
    public string IdentityGuid { get; private set; }

    private List<PaymentMethod> _paymentMethods = new List<PaymentMethod>();

    public IEnumerable<PaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

#pragma warning disable CS8618 // Required by Entity Framework
    private Buyer() { }
#pragma warning restore CS8618

    public Buyer(string identity) : this()
    {
        Guard.Against.NullOrEmpty(identity, nameof(identity));
        IdentityGuid = identity;
    }

    /// <summary>Saves a card for this buyer and returns the new payment method.</summary>
    public PaymentMethod AddPaymentMethod(string cardId, string last4, string brand,
        string expiryYearMonth, string? alias)
    {
        var method = new PaymentMethod(cardId, last4, brand, expiryYearMonth, alias);
        _paymentMethods.Add(method);
        return method;
    }

    /// <summary>
    /// Removes one of this buyer's saved cards. Throws if the id is not one of this buyer's
    /// cards, so a shopper can never delete another's card even by id-guessing.
    /// </summary>
    public PaymentMethod RemovePaymentMethod(int paymentMethodId)
    {
        var method = _paymentMethods.FirstOrDefault(pm => pm.Id == paymentMethodId);
        if (method is null)
        {
            throw new PaymentMethodNotFoundException(paymentMethodId);
        }
        _paymentMethods.Remove(method);
        return method;
    }
}
