using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class CancelOrderRequest
{
    public int OrderId { get; set; }
}

public class CancelOrderResponse
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Operator action: cancels an order before fulfilment by voiding the PayPal authorization, so
/// no money ever moves. Rejects orders that have already been captured (use refunds instead).
/// </summary>
public class CancelOrderEndpoint : IEndpoint<IResult, CancelOrderRequest, OrderEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/cancel",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, OrderEndpointServices services) =>
            {
                return await HandleAsync(new CancelOrderRequest { OrderId = orderId }, services);
            })
            .Produces<CancelOrderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(CancelOrderRequest request, OrderEndpointServices services)
    {
        var order = await services.OrderRepository.GetByIdAsync(request.OrderId);
        if (order is null)
            return Results.NotFound();

        if (order.Status == OrderStatus.Cancelled)
            return Results.Ok(new CancelOrderResponse { OrderId = order.Id, Status = order.Status.ToString() }); // idempotent replay

        if (order.Status != OrderStatus.Authorized)
            return Results.Conflict($"Order {order.Id} is {order.Status}; only an authorized (not yet fulfilled) order can be cancelled.");

        var payment = await services.PaymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(request.OrderId));
        if (payment?.AuthorizationId is null)
            return Results.Conflict($"Order {order.Id} has no recorded authorization to release.");

        await services.PaymentGateway.VoidAsync(order.Id, payment.AuthorizationId);
        payment.RecordVoid();
        await services.PaymentRepository.UpdateAsync(payment);

        order.MarkCancelled();
        await services.OrderRepository.UpdateAsync(order);

        return Results.Ok(new CancelOrderResponse { OrderId = order.Id, Status = order.Status.ToString() });
    }
}
