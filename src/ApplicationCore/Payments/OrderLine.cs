namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>A requested order line: a catalog item id and a quantity. Price comes from the catalog, never the caller.</summary>
public readonly record struct OrderLine(int CatalogItemId, int Quantity);
