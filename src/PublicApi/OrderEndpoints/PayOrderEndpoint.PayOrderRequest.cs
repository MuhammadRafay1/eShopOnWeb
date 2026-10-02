namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class PayOrderCardRequest
{
    public string Number { get; set; } = string.Empty;

    /// <summary>ISO-8601 year-month, e.g. "2030-01".</summary>
    public string ExpiryYearMonth { get; set; } = string.Empty;
    public string SecurityCode { get; set; } = string.Empty;
    public string? CardholderName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}

public class PayOrderBody
{
    /// <summary>One-off card details. Mutually exclusive with <see cref="PaymentMethodId"/>.</summary>
    public PayOrderCardRequest? Card { get; set; }

    /// <summary>Pay with a previously saved card instead of entering card details again.</summary>
    public int? PaymentMethodId { get; set; }
}

public class PayOrderRequest : BaseRequest
{
    public int OrderId { get; }
    public string BuyerId { get; }
    public PayOrderBody Body { get; }

    public PayOrderRequest(int orderId, string buyerId, PayOrderBody body)
    {
        OrderId = orderId;
        BuyerId = buyerId;
        Body = body;
    }
}
