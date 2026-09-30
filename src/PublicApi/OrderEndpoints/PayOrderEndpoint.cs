using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;
using AppBillingAddress = Microsoft.eShopWeb.ApplicationCore.Interfaces.BillingAddress;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CardDto
{
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public string Expiry { get; set; } = "";       // "YYYY-MM"
    public string SecurityCode { get; set; } = "";
    public BillingAddressDto? BillingAddress { get; set; }
}

public class BillingAddressDto
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = "";
}

public class PayOrderRequest : BaseRequest
{
    public CardDto? Card { get; set; }
    public int? PaymentMethodId { get; set; }

    [JsonIgnore] public string BuyerId { get; set; } = "";
    [JsonIgnore] public int OrderId { get; set; }
}

public class PayOrderResponse : BaseResponse
{
    public PayOrderResponse(System.Guid correlationId) : base(correlationId) { }
    public PayOrderResponse() { }

    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public PaymentDto? Payment { get; set; }
}

/// <summary>
/// POST api/orders/{orderId}/pay — authorize (hold, do not capture) the order total, with a one-off
/// card or a saved card. Shopper-scoped; acts only on the caller's own order. Idempotent in effect.
/// </summary>
public class PayOrderEndpoint : IEndpoint<IResult, PayOrderRequest, IRepository<Order>>
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentMethod> _paymentMethodRepository;
    private readonly IPaymentGatewayService _gateway;
    private readonly PayPalOptions _payPalOptions;

    public PayOrderEndpoint(IRepository<Payment> paymentRepository,
        IRepository<PaymentMethod> paymentMethodRepository,
        IPaymentGatewayService gateway,
        IOptions<PayPalOptions> payPalOptions)
    {
        _paymentRepository = paymentRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _gateway = gateway;
        _payPalOptions = payPalOptions.Value;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/pay",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, PayOrderRequest request, ClaimsPrincipal user, IRepository<Order> orderRepository) =>
            {
                request.OrderId = orderId;
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, orderRepository);
            })
            .Produces<PayOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PayOrderRequest request, IRepository<Order> orderRepository)
    {
        // Load with items so order.Total() is computed from the full order (GetByIdAsync omits includes).
        var order = await orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpecification(request.OrderId));
        if (order is null || order.BuyerId != request.BuyerId)
            return Results.NotFound();

        bool hasCard = request.Card is not null;
        bool hasSaved = request.PaymentMethodId is not null;
        if (hasCard == hasSaved)
            return Results.BadRequest("Provide exactly one of 'card' or 'paymentMethodId'.");

        var response = new PayOrderResponse(request.CorrelationId()) { OrderId = order.Id };

        // Idempotent-in-effect: a double-click on an already-authorized order returns the stored
        // payment, without calling PayPal again. A cancelled order can no longer be paid.
        if (order.Status == OrderStatus.Cancelled)
            throw new InvalidOrderStateException($"Order {order.Id} was cancelled and can no longer be paid.");
        if (!order.CurrentlyPayable)
        {
            var existing = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(order.Id));
            response.Status = order.Status.ToString();
            response.Payment = existing is null ? null : PaymentDto.From(existing);
            return Results.Ok(response);
        }

        string? vaultId = null;
        AppBillingAddress? billing = null;
        CardDetails? card = null;

        if (hasSaved)
        {
            var method = await _paymentMethodRepository.GetByIdAsync(request.PaymentMethodId!.Value);
            if (method is null || method.BuyerId != request.BuyerId)
                return Results.NotFound();
            vaultId = method.PayPalVaultId;
        }
        else
        {
            var b = request.Card!.BillingAddress;
            if (b is not null)
                billing = new AppBillingAddress(b.Line1, b.Line2, b.City, b.State, b.PostalCode, b.CountryCode);
            card = new CardDetails(request.Card!.Name, request.Card!.Number, request.Card!.Expiry,
                request.Card!.SecurityCode, billing);
        }

        var currency = _payPalOptions.Currency;
        var authRequest = new AuthorizationRequest(order.Total(), currency, card, vaultId,
            IdempotencyKeyBase: $"order:{order.Id}");

        var result = await _gateway.AuthorizeAsync(authRequest);

        if (result.PayerActionRequired)
        {
            // Task's explicit "stop and report" case — this API does not implement a browser approval flow.
            return Results.Json(new
            {
                message = "This card requires shopper approval in a browser, which this API does not support. Try a different card.",
                orderId = order.Id
            }, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var payment = new Payment(order.Id, currency, order.Total(), result.PayPalOrderId!,
            result.AuthorizationId!, result.Status ?? "", result.ExpiresAt);
        await _paymentRepository.AddAsync(payment);

        order.MarkPaymentAuthorized();
        await orderRepository.UpdateAsync(order);

        response.Status = order.Status.ToString();
        response.Payment = PaymentDto.From(payment);
        return Results.Ok(response);
    }
}
