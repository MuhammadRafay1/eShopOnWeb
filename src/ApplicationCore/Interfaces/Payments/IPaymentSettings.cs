namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

/// <summary>The subset of PayPal configuration ApplicationCore needs, without depending on Infrastructure.</summary>
public interface IPaymentSettings
{
    string CurrencyCode { get; }
}
