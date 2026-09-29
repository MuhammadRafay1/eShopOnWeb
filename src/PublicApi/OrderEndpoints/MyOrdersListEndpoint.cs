using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
using Microsoft.eShopWeb.Infrastructure.Payments.PayPal;
using Microsoft.eShopWeb.PublicApi.PaymentModels;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>Returns the caller's own orders with their payment state.</summary>
public class MyOrdersListEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/my-orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                IReadRepository<Order> orderRepository,
                IReadRepository<Payment> paymentRepository,
                IOptions<PayPalOptions> payPalOptions,
                ClaimsPrincipal user) =>
            {
                return await HandleAsync(orderRepository, paymentRepository, payPalOptions, user);
            })
            .Produces<IReadOnlyList<OrderView>>()
            .WithTags("OrderEndpoints");
    }

    private static async Task<IResult> HandleAsync(IReadRepository<Order> orderRepository,
        IReadRepository<Payment> paymentRepository, IOptions<PayPalOptions> payPalOptions,
        ClaimsPrincipal user)
    {
        var buyerId = user.Identity!.Name!;

        var orders = await orderRepository.ListAsync(new CustomerOrdersWithItemsSpecification(buyerId));
        var orderIds = orders.Select(o => o.Id).ToList();

        var payments = orderIds.Count == 0
            ? new List<Payment>()
            : (await paymentRepository.ListAsync(new PaymentsByOrderIdsSpecification(orderIds))).ToList();
        var paymentsByOrder = payments.ToDictionary(p => p.OrderId);

        var currency = payPalOptions.Value.Currency;
        var result = orders
            .OrderByDescending(o => o.OrderDate)
            .Select(o => OrderView.From(o, paymentsByOrder.GetValueOrDefault(o.Id), currency))
            .ToList();

        return Results.Ok(result);
    }
}
