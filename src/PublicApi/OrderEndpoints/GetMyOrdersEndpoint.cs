using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class GetMyOrdersRequest : BaseRequest
{
    [JsonIgnore] public string BuyerId { get; set; } = "";
}

public class MyOrderDto
{
    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public string Currency { get; set; } = "";
    public List<OrderItemDto> Items { get; set; } = new();
    public PaymentDto? Payment { get; set; }
}

public class GetMyOrdersResponse : BaseResponse
{
    public GetMyOrdersResponse(Guid correlationId) : base(correlationId) { }
    public GetMyOrdersResponse() { }

    public List<MyOrderDto> Orders { get; set; } = new();
}

/// <summary>
/// GET api/my-orders — the caller's own orders with their payment state.
/// </summary>
public class GetMyOrdersEndpoint : IEndpoint<IResult, GetMyOrdersRequest, IRepository<Order>>
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly PayPalOptions _payPalOptions;

    public GetMyOrdersEndpoint(IRepository<Payment> paymentRepository, IOptions<PayPalOptions> payPalOptions)
    {
        _paymentRepository = paymentRepository;
        _payPalOptions = payPalOptions.Value;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IRepository<Order> orderRepository) =>
            {
                return await HandleAsync(new GetMyOrdersRequest { BuyerId = user.Identity!.Name! }, orderRepository);
            })
            .Produces<GetMyOrdersResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(GetMyOrdersRequest request, IRepository<Order> orderRepository)
    {
        var response = new GetMyOrdersResponse(request.CorrelationId());

        var orders = await orderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(request.BuyerId));
        var orderIds = orders.Select(o => o.Id).ToArray();

        var payments = orderIds.Length == 0
            ? new List<Payment>()
            : (await _paymentRepository.ListAsync(new PaymentsByOrderIdsSpecification(orderIds))).ToList();
        var paymentsByOrder = payments.ToDictionary(p => p.OrderId);

        response.Orders = orders.Select(order =>
        {
            paymentsByOrder.TryGetValue(order.Id, out var payment);
            return new MyOrderDto
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                Total = order.Total(),
                Currency = payment?.Currency ?? _payPalOptions.Currency,
                Items = PaymentDto.ItemsOf(order),
                Payment = payment is null ? null : PaymentDto.From(payment)
            };
        }).ToList();

        return Results.Ok(response);
    }
}
