namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Supplies the configured payment currency (PayPal:Currency). Implemented in Infrastructure.</summary>
public interface ICurrencyProvider
{
    string CurrencyCode { get; }
}
