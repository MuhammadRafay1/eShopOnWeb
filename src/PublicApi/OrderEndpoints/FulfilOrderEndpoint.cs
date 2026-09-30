using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class FulfilOrderRequest
{
    public int OrderId { get; set; }
}

public class FulfilOrderResponse
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CaptureId { get; set; }
    public string? CaptureStatus { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

/// <summary>
/// Operator action: marks the order fulfilled, which is when the held authorization is actually
/// captured. If the authorization has gone stale it is renewed once before capture; if it can no
/// longer be renewed, a 409 with an operator-actionable message is returned instead of failing silently.
/// </summary>
public class FulfilOrderEndpoint : IEndpoint<IResult, FulfilOrderRequest, OrderEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders/{orderId}/fulfil",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, OrderEndpointServices services) =>
            {
                return await HandleAsync(new FulfilOrderRequest { OrderId = orderId }, services);
            })
            .Produces<FulfilOrderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(FulfilOrderRequest request, OrderEndpointServices services)
    {
        var order = await services.OrderRepository.GetByIdAsync(request.OrderId);
        if (order is null)
            return Results.NotFound();

        var payment = await services.PaymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(request.OrderId));

        if (order.Status == OrderStatus.Fulfilled && payment is not null)
            return Results.Ok(ToResponse(order, payment)); // idempotent replay

        if (order.Status != OrderStatus.Authorized || payment?.AuthorizationId is null)
            return Results.Conflict($"Order {order.Id} is {order.Status} and is not ready to be fulfilled.");

        var authorizationId = payment.AuthorizationId;

        if (payment.AuthorizationExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
        {
            authorizationId = await RenewAsync(order.Id, payment, services);
        }

        try
        {
            var capture = await services.PaymentGateway.CaptureAsync(order.Id, authorizationId);
            payment.RecordCapture(capture.CaptureId, capture.Status, capture.CapturedAmount, capture.Fee, capture.NetAmount);
        }
        catch (PayPalGatewayException ex) when (IsExpiredAuthorizationIssue(ex))
        {
            // PayPal's own clock disagreed with ours - renew once and retry.
            authorizationId = await RenewAsync(order.Id, payment, services);
            var capture = await services.PaymentGateway.CaptureAsync(order.Id, authorizationId);
            payment.RecordCapture(capture.CaptureId, capture.Status, capture.CapturedAmount, capture.Fee, capture.NetAmount);
        }

        await services.PaymentRepository.UpdateAsync(payment);

        order.MarkFulfilled();
        await services.OrderRepository.UpdateAsync(order);

        return Results.Ok(ToResponse(order, payment));
    }

    private static async Task<string> RenewAsync(int orderId, ApplicationCore.Entities.PaymentAggregate.Payment payment, OrderEndpointServices services)
    {
        try
        {
            var reauthorization = await services.PaymentGateway.ReauthorizeAsync(orderId, payment.AuthorizationId!, payment.AuthorizedAmount);
            payment.RecordReauthorization(reauthorization.AuthorizationId, reauthorization.Status, reauthorization.ExpiresAt, reauthorization.Amount);
            await services.PaymentRepository.UpdateAsync(payment);
            return reauthorization.AuthorizationId;
        }
        catch (PayPalGatewayException ex)
        {
            throw new AuthorizationRenewalFailedException(orderId, ex.Message);
        }
    }

    private static bool IsExpiredAuthorizationIssue(PayPalGatewayException ex) =>
        ex.Issue?.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase) ?? false;

    private static FulfilOrderResponse ToResponse(Order order, ApplicationCore.Entities.PaymentAggregate.Payment payment) => new()
    {
        OrderId = order.Id,
        Status = order.Status.ToString(),
        CaptureId = payment.CaptureId,
        CaptureStatus = payment.CaptureStatus,
        CapturedAmount = payment.CapturedAmount,
        PayPalFee = payment.PayPalFee,
        NetAmount = payment.NetAmount,
        Currency = payment.Currency
    };
}
