using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public class PayPalGatewaySettings : IPaymentGatewaySettings
{
    private readonly PayPalSettings _settings;

    public PayPalGatewaySettings(IOptions<PayPalSettings> options)
    {
        _settings = options.Value;
    }

    public string CurrencyCode => _settings.Currency;
}
