using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace Microsoft.eShopWeb.Infrastructure.Payments.PayPal;

/// <summary>
/// Saved-card vaulting against PayPal Vault v3.
/// </summary>
public class PayPalVaultGateway : IPayPalVaultGateway
{
    private readonly PayPalApiClient _api;

    public PayPalVaultGateway(PayPalApiClient api)
    {
        _api = api;
    }

    public async Task<PayPalSavedCard> SaveCardAsync(string customerId, CardDetails card, CancellationToken ct)
    {
        var wire = new VaultTokenRequestWire
        {
            Customer = new CustomerWire { Id = customerId },
            PaymentSource = new PaymentSourceWire
            {
                Card = new CardWire
                {
                    Name = card.Name,
                    Number = card.Number,
                    Expiry = card.Expiry,
                    SecurityCode = card.SecurityCode,
                    BillingAddress = PayPalOrdersGateway.ToBillingAddress(card.BillingAddress)
                }
            }
        };

        var response = await _api.PostAsync<VaultTokenResponseWire>(
            "v3/vault/payment-tokens", wire, idempotencyKey: null, ct);

        if (!response.IsSuccess)
        {
            // A 4xx here means PayPal rejected the card details themselves.
            throw new PaymentDeclinedException(
                response.Error?.Describe() ?? "PayPal could not save the card.");
        }

        var token = response.Value
            ?? throw new PayPalIntegrationException("PayPal returned an empty vault response.");
        if (string.IsNullOrEmpty(token.Id))
        {
            throw new PayPalIntegrationException("PayPal vault response did not contain a token id.");
        }

        var savedCard = token.PaymentSource?.Card;
        return new PayPalSavedCard
        {
            VaultId = token.Id,
            Last4 = savedCard?.LastDigits ?? string.Empty,
            Brand = savedCard?.Brand ?? string.Empty,
            ExpiryYearMonth = savedCard?.Expiry ?? card.Expiry,
            CardholderName = savedCard?.Name
        };
    }

    public async Task DeletePaymentTokenAsync(string vaultId, CancellationToken ct)
    {
        var response = await _api.DeleteAsync($"v3/vault/payment-tokens/{vaultId}", ct);
        if (!response.IsSuccess)
        {
            throw new PayPalIntegrationException(
                $"PayPal could not delete vault token {vaultId}: {response.Error?.Describe()}");
        }
    }
}
