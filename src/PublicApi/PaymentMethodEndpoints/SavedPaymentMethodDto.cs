using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>Safe enough to recognise the card by, never full card details.</summary>
public class SavedPaymentMethodDto
{
    public int PaymentMethodId { get; set; }
    public string Brand { get; set; } = default!;
    public string Last4 { get; set; } = default!;
    public string Expiry { get; set; } = default!;
    public string? Label { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static SavedPaymentMethodDto FromDomain(SavedPaymentMethod method) => new()
    {
        PaymentMethodId = method.Id,
        Brand = method.Brand,
        Last4 = method.Last4,
        Expiry = method.Expiry,
        Label = method.Label,
        CreatedAt = method.CreatedAt
    };
}
