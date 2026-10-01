namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>A currency amount. Provider-agnostic; the Infrastructure layer converts to/from PayPal's wire shape.</summary>
public readonly record struct Money(decimal Amount, string CurrencyCode);
