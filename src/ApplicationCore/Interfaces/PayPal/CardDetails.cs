namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Raw card input, mapped 1:1 onto PayPal's card_request / payment_source.card schema
/// (name, number, expiry as YYYY-MM, security_code, billing_address). Never persisted in this
/// app's own database and never logged — it flows through only to PayPal.
/// </summary>
public class CardDetails
{
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";

    /// <summary>Expiry in PayPal's date_year_month format: YYYY-MM (e.g. 2028-04).</summary>
    public string Expiry { get; set; } = "";
    public string? SecurityCode { get; set; }
    public CardBillingAddress? BillingAddress { get; set; }
}

/// <summary>Billing address, mapped onto PayPal's card billing_address (address) schema.</summary>
public class CardBillingAddress
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }

    /// <summary>City. Maps to PayPal admin_area_2.</summary>
    public string? AdminArea2 { get; set; }

    /// <summary>State/province. Maps to PayPal admin_area_1.</summary>
    public string? AdminArea1 { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>Two-letter ISO country code. Required by PayPal's address schema.</summary>
    public string CountryCode { get; set; } = "";
}
