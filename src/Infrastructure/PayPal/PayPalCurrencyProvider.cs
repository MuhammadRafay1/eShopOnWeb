using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>Exposes the configured PayPal currency (PayPal:Currency) to the application layer.</summary>
public sealed class PayPalCurrencyProvider : ICurrencyProvider
{
    public PayPalCurrencyProvider(IOptions<PayPalOptions> options)
    {
        CurrencyCode = options.Value.Currency;
    }

    public string CurrencyCode { get; }
}
