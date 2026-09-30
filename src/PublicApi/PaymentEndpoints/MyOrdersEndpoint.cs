using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class MyOrderDto
{
    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public string Currency { get; set; } = "";
    public decimal? CapturedAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal? TotalRefundedAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class MyOrdersResponse
{
    public List<MyOrderDto> Orders { get; set; } = new();
}

/// <summary>Returns the signed-in shopper's own orders with their payment state.</summary>
public class MyOrdersEndpoint : IEndpoint<IResult, IReadRepository<Order>>
{
    private readonly IReadRepository<OrderPayment> _paymentRepository;

    public MyOrdersEndpoint(IReadRepository<OrderPayment> paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IReadRepository<Order> orderRepository, CancellationToken ct) =>
            {
                return await HandleAsync(user.Identity!.Name!, orderRepository, ct);
            })
            .Produces<MyOrdersResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public Task<IResult> HandleAsync(IReadRepository<Order> orderRepository) =>
        throw new NotSupportedException("Use the route handler, which supplies the buyer identity.");

    private async Task<IResult> HandleAsync(string buyerId, IReadRepository<Order> orderRepository, CancellationToken ct)
    {
        var orders = await orderRepository.ListAsync(new OrdersByBuyerIdSpecification(buyerId), ct);
        var orderIds = orders.Select(o => o.Id).ToArray();

        var payments = orderIds.Length == 0
            ? new List<OrderPayment>()
            : (await _paymentRepository.ListAsync(new OrderPaymentsByOrderIdsSpecification(orderIds), ct)).ToList();
        var paymentByOrderId = payments.ToDictionary(p => p.OrderId);

        var response = new MyOrdersResponse
        {
            Orders = orders.Select(o =>
            {
                paymentByOrderId.TryGetValue(o.Id, out var payment);
                return new MyOrderDto
                {
                    OrderId = o.Id,
                    Status = o.Status.ToString(),
                    Total = o.Total(),
                    Currency = o.Currency,
                    CapturedAmount = payment?.CapturedAmount,
                    NetAmount = payment?.NetAmount,
                    TotalRefundedAmount = payment is null ? null : payment.TotalRefundedAmount,
                    CreatedAt = o.OrderDate
                };
            }).ToList()
        };
        return Results.Ok(response);
    }
}
