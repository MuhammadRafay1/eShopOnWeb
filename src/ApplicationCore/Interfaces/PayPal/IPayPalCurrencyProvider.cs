namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Exposes the single configured PayPal currency (PayPal:Currency) to the domain layer without
/// making ApplicationCore depend on Infrastructure's options type. Catalog prices are treated as
/// already denominated in this currency (no FX).
/// </summary>
public interface IPayPalCurrencyProvider
{
    string Currency { get; }
}
