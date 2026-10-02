using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.EntityFrameworkCore;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Operator action: cancels an order before it is fulfilled, releasing the shopper's held funds —
/// no money ever moves.
/// </summary>
public class CancelOrderEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId:int}/cancel",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, IRepository<Order> orderRepository, IPayPalPaymentGateway gateway) =>
            {
                return await HandleAsync(orderId, orderRepository, gateway);
            })
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(int orderId, IRepository<Order> orderRepository, IPayPalPaymentGateway gateway)
    {
        var order = await orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
        if (order?.Payment is null)
        {
            return Results.NotFound();
        }

        try
        {
            order.BeginCancel();
        }
        catch (System.InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }

        try
        {
            await orderRepository.UpdateAsync(order);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict("This order is already being cancelled or fulfilled.");
        }

        try
        {
            await gateway.VoidAsync(order.Payment.AuthorizationId!, default);
        }
        catch (PaymentGatewayException ex)
        {
            order.RecordCancelFailed(ex.Message);
            await orderRepository.UpdateAsync(order);
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }

        order.RecordCancelled();
        await orderRepository.UpdateAsync(order);

        return Results.Ok(new { orderId = order.Id, status = order.Status.ToString() });
    }
}
