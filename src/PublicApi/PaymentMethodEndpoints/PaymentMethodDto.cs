using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>A safe-to-show description of a saved card - never full card details.</summary>
public class PaymentMethodDto
{
    public int PaymentMethodId { get; set; }
    public string? Brand { get; set; }
    public string? LastDigits { get; set; }
    public string? Expiry { get; set; }
    public string? CardholderName { get; set; }

    public static PaymentMethodDto From(VaultedPaymentMethod paymentMethod) => new()
    {
        PaymentMethodId = paymentMethod.Id,
        Brand = paymentMethod.Brand,
        LastDigits = paymentMethod.LastDigits,
        Expiry = paymentMethod.Expiry,
        CardholderName = paymentMethod.CardholderName
    };
}
