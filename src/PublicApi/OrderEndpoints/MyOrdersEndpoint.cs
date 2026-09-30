using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class MyOrdersRequest
{
    public string BuyerId { get; set; } = string.Empty;
}

public class MyOrderItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}

public class MyOrderRefundDto
{
    public string RefundId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class MyOrderDto
{
    public int OrderId { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<MyOrderItemDto> Items { get; set; } = new();

    public string? AuthorizationId { get; set; }
    public string? AuthorizationStatus { get; set; }
    public string? CaptureId { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal RefundedTotal { get; set; }
    public List<MyOrderRefundDto> Refunds { get; set; } = new();
}

public class MyOrdersResponse
{
    public List<MyOrderDto> Orders { get; set; } = new();
}

/// <summary>The caller's own orders together with their payment state. Never another shopper's.</summary>
public class MyOrdersEndpoint : IEndpoint<IResult, MyOrdersRequest, OrderEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, OrderEndpointServices services) =>
            {
                return await HandleAsync(new MyOrdersRequest { BuyerId = user.Identity!.Name! }, services);
            })
            .Produces<MyOrdersResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(MyOrdersRequest request, OrderEndpointServices services)
    {
        var orders = await services.OrderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(request.BuyerId));
        var orderIds = orders.Select(o => o.Id).ToList();
        var payments = orderIds.Count == 0
            ? new List<Payment>()
            : await services.PaymentRepository.ListAsync(new PaymentsByOrderIdsSpecification(orderIds));
        var paymentsByOrderId = payments.ToDictionary(p => p.OrderId);

        var response = new MyOrdersResponse
        {
            Orders = orders.Select(order =>
            {
                paymentsByOrderId.TryGetValue(order.Id, out var payment);
                return new MyOrderDto
                {
                    OrderId = order.Id,
                    OrderDate = order.OrderDate,
                    Total = order.Total(),
                    Status = order.Status.ToString(),
                    Items = order.OrderItems.Select(i => new MyOrderItemDto
                    {
                        ProductName = i.ItemOrdered.ProductName,
                        UnitPrice = i.UnitPrice,
                        Units = i.Units
                    }).ToList(),
                    AuthorizationId = payment?.AuthorizationId,
                    AuthorizationStatus = payment?.AuthorizationStatus,
                    CaptureId = payment?.CaptureId,
                    CapturedAmount = payment?.CapturedAmount,
                    PayPalFee = payment?.PayPalFee,
                    NetAmount = payment?.NetAmount,
                    RefundedTotal = payment?.RefundedTotal ?? 0m,
                    Refunds = payment?.Refunds.Select(r => new MyOrderRefundDto { RefundId = r.RefundId, Amount = r.Amount, Status = r.Status }).ToList() ?? new()
                };
            }).ToList()
        };

        return Results.Ok(response);
    }
}
