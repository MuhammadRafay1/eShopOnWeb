namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

/// <summary>
/// What to pay an order authorization with: exactly one of <see cref="Card"/> (a one-off card) or
/// <see cref="SavedPaymentMethodId"/> (one of the shopper's saved cards) must be set.
/// </summary>
public record PaymentInstrument(PayPalCardDetails? Card, int? SavedPaymentMethodId);
