namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>The non-secret payment-gateway settings ApplicationCore needs, without depending on Infrastructure.</summary>
public interface IPaymentGatewaySettings
{
    string CurrencyCode { get; }
}
