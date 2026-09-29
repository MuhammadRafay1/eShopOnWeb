using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>Input for creating a PayPal order to authorize (checkout_orders_v2).</summary>
public class CreateOrderInput
{
    /// <summary>The eShop order id — used for PayPal-Request-Id, invoice_id and custom_id correlation.</summary>
    public int OrderId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal Amount { get; set; }

    /// <summary>Raw card for a one-off payment (mutually exclusive with <see cref="VaultId"/>).</summary>
    public CardDetails? Card { get; set; }

    /// <summary>A saved-card vault token id to pay with (mutually exclusive with <see cref="Card"/>).</summary>
    public string? VaultId { get; set; }
}

/// <summary>Wraps the Orders v2 API: create the PayPal order and authorize it (place the hold).</summary>
public interface IPayPalOrdersClient
{
    /// <summary>POST /v2/checkout/orders with intent=AUTHORIZE and a card/vault payment source.</summary>
    Task<PayPalOrderResult> CreateOrderAsync(CreateOrderInput input, CancellationToken cancellationToken = default);

    /// <summary>POST /v2/checkout/orders/{id}/authorize — places the hold and returns the authorization.</summary>
    Task<PayPalAuthorizationResult> AuthorizeOrderAsync(int orderId, string payPalOrderId, CancellationToken cancellationToken = default);

    /// <summary>GET /v2/checkout/orders/{id}.</summary>
    Task<PayPalOrderResult> GetOrderAsync(string payPalOrderId, CancellationToken cancellationToken = default);
}
