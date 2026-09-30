namespace Microsoft.eShopWeb.ApplicationCore.Payments;

public record OrderLineRequest(int CatalogItemId, int Quantity);
