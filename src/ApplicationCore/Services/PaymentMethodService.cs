using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentMethodService : IPaymentMethodService
{
    // PayPal's merchant_customer_id allows up to 64 chars (pattern ^[0-9a-zA-Z-_.^*$@#]+$).
    private const int MaxMerchantCustomerIdLength = 64;

    private readonly IRepository<Buyer> _buyerRepository;
    private readonly IPayPalVaultClient _vaultClient;

    public PaymentMethodService(IRepository<Buyer> buyerRepository, IPayPalVaultClient vaultClient)
    {
        _buyerRepository = buyerRepository;
        _vaultClient = vaultClient;
    }

    public async Task<PaymentMethod> SaveAsync(string buyerId, CardDetails card)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(card, nameof(card));
        if (buyerId.Length > MaxMerchantCustomerIdLength)
        {
            throw new InvalidPaymentRequestException(
                $"Buyer id exceeds PayPal's {MaxMerchantCustomerIdLength}-character customer id limit.");
        }

        // Vault the card in PayPal first — a per-request idempotency key guards against a double
        // submit creating two tokens for the same inbound call.
        var requestId = $"vault-{buyerId}-{System.Guid.NewGuid():N}";
        var token = await _vaultClient.CreatePaymentTokenAsync(buyerId, card, requestId);

        var buyer = await GetOrCreateBuyerAsync(buyerId);

        var paymentMethod = new PaymentMethod(
            vaultId: token.Id,
            brand: token.Brand ?? "UNKNOWN",
            last4: token.LastDigits ?? "",
            expiryYearMonth: token.Expiry ?? card.Expiry,
            alias: null);

        buyer.AddPaymentMethod(paymentMethod);
        await _buyerRepository.UpdateAsync(buyer);

        return paymentMethod;
    }

    public async Task<IReadOnlyList<PaymentMethod>> ListAsync(string buyerId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        var buyer = await _buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(buyerId));
        return buyer?.PaymentMethods.ToList() ?? new List<PaymentMethod>();
    }

    public async Task DeleteAsync(string buyerId, int paymentMethodId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        var buyer = await _buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(buyerId));
        var method = buyer?.PaymentMethods.FirstOrDefault(pm => pm.Id == paymentMethodId);
        if (buyer is null || method is null)
        {
            // Same exception whether the card is missing or owned by someone else.
            throw new PaymentMethodNotFoundException(paymentMethodId);
        }

        // Remove from PayPal's vault (idempotent — a 404 is treated as success by the client), then
        // drop the local reference so it can no longer be listed or used to pay.
        if (!string.IsNullOrEmpty(method.CardId))
        {
            await _vaultClient.DeletePaymentTokenAsync(method.CardId);
        }

        buyer.RemovePaymentMethod(paymentMethodId);
        await _buyerRepository.UpdateAsync(buyer);
    }

    private async Task<Buyer> GetOrCreateBuyerAsync(string buyerId)
    {
        var buyer = await _buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(buyerId));
        if (buyer is null)
        {
            buyer = await _buyerRepository.AddAsync(new Buyer(buyerId));
        }
        return buyer;
    }
}
