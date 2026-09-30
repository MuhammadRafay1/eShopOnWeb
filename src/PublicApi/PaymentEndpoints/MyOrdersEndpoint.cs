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

public class MyOrderItemModel
{
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}

public class MyOrderModel
{
    public int OrderId { get; set; }
    public System.DateTimeOffset OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<MyOrderItemModel> Items { get; set; } = new();
    public PaymentView? Payment { get; set; }
}

public class MyOrdersResponse
{
    public List<MyOrderModel> Orders { get; set; } = new();
}

/// <summary>
/// Lists the caller's own orders with their payment state (status, captured/refunded amounts,
/// currency). Shopper-scoped.
/// </summary>
public class MyOrdersEndpoint : IEndpoint<IResult, string, IReadRepository<Order>>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IReadRepository<Order> orderRepository, CancellationToken ct) =>
            {
                return await HandleAsync(user.Identity!.Name!, orderRepository, ct);
            })
            .Produces<MyOrdersResponse>()
            .WithTags("Orders");
    }

    public Task<IResult> HandleAsync(string buyerId, IReadRepository<Order> orderRepository)
        => HandleAsync(buyerId, orderRepository, CancellationToken.None);

    public async Task<IResult> HandleAsync(string buyerId, IReadRepository<Order> orderRepository, CancellationToken ct)
    {
        var orders = await orderRepository.ListAsync(new CustomerOrdersWithPaymentSpecification(buyerId), ct);

        var response = new MyOrdersResponse
        {
            Orders = orders.Select(o => new MyOrderModel
            {
                OrderId = o.Id,
                OrderDate = o.OrderDate,
                Status = o.Status.ToString(),
                Total = o.Total(),
                Items = o.OrderItems.Select(i => new MyOrderItemModel
                {
                    CatalogItemId = i.ItemOrdered.CatalogItemId,
                    ProductName = i.ItemOrdered.ProductName,
                    UnitPrice = i.UnitPrice,
                    Units = i.Units
                }).ToList(),
                Payment = o.Payment is null ? null : PaymentView.From(o.Payment)
            }).ToList()
        };
        return Results.Ok(response);
    }
}
