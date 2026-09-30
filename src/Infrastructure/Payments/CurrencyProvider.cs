using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Payments;

public class CurrencyProvider : ICurrencyProvider
{
    private readonly PayPalOptions _options;

    public CurrencyProvider(IOptions<PayPalOptions> options)
    {
        _options = options.Value;
    }

    public string CurrencyCode => _options.Currency;
}
