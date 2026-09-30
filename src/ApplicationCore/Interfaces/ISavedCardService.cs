using System.Collections.Generic;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface ISavedCardService
{
    Task<Result<SavedPaymentMethod>> SaveCardAsync(string buyerId, PayPalCardDetails card, string? label);

    Task<IReadOnlyList<SavedPaymentMethod>> ListAsync(string buyerId);

    Task<Result> DeleteAsync(int paymentMethodId, string buyerId);
}
