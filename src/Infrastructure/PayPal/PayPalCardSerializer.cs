using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Builds the PayPal card object (card_request / payment_source.card) from raw card details, mapped
/// 1:1 onto the spec's snake_case fields.
///
/// SECURITY: this is the ONE place raw card data (number / security_code) is serialized. The result
/// is sent directly to PayPal and MUST NEVER be logged. Do not add a log statement that captures
/// the dictionary this method populates.
/// </summary>
internal static class PayPalCardSerializer
{
    public static void PopulateRawCard(Dictionary<string, object?> card, CardDetails details)
    {
        card["name"] = details.Name;
        card["number"] = details.Number;
        card["expiry"] = details.Expiry;         // YYYY-MM
        if (!string.IsNullOrEmpty(details.SecurityCode))
        {
            card["security_code"] = details.SecurityCode;
        }

        if (details.BillingAddress is { } addr)
        {
            var billing = new Dictionary<string, object?>
            {
                ["country_code"] = addr.CountryCode
            };
            if (!string.IsNullOrEmpty(addr.AddressLine1)) billing["address_line_1"] = addr.AddressLine1;
            if (!string.IsNullOrEmpty(addr.AddressLine2)) billing["address_line_2"] = addr.AddressLine2;
            if (!string.IsNullOrEmpty(addr.AdminArea2)) billing["admin_area_2"] = addr.AdminArea2;
            if (!string.IsNullOrEmpty(addr.AdminArea1)) billing["admin_area_1"] = addr.AdminArea1;
            if (!string.IsNullOrEmpty(addr.PostalCode)) billing["postal_code"] = addr.PostalCode;
            card["billing_address"] = billing;
        }
    }
}
