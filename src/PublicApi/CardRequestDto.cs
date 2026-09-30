using Microsoft.eShopWeb.ApplicationCore.PayPal;

namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// A raw card submitted by a caller - either for a one-off payment (<see cref="OrderEndpoints.PayOrderRequest"/>)
/// or to save a new card (<see cref="PaymentMethodEndpoints.CreatePaymentMethodRequest"/>). Never stored;
/// converted straight into a <see cref="PayPalCardDetails"/> and discarded.
/// </summary>
public class CardRequestDto
{
    public string Number { get; set; } = default!;
    public string Expiry { get; set; } = default!; // YYYY-MM
    public string Cvc { get; set; } = default!;
    public string Name { get; set; } = default!;
    public BillingAddressRequestDto BillingAddress { get; set; } = default!;

    public PayPalCardDetails ToDomain() => new(
        Number,
        Expiry,
        Cvc,
        Name,
        new PayPalBillingAddress(BillingAddress.Street, BillingAddress.City, BillingAddress.State, BillingAddress.ZipCode, BillingAddress.Country));
}

public class BillingAddressRequestDto
{
    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string? State { get; set; }
    public string Country { get; set; } = default!; // ISO 3166-1 alpha-2, e.g. "US"
    public string ZipCode { get; set; } = default!;
}
