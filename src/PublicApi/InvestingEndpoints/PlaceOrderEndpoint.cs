using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class PlaceOrderRequest : BaseRequest
{
    public List<PlaceOrderItem> Items { get; set; } = new();

    /// <summary>Set from the caller's token; never trusted from the request body.</summary>
    [JsonIgnore]
    public string BuyerId { get; set; } = string.Empty;
}

public class PlaceOrderItem
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class PlaceOrderResponse : BaseResponse
{
    public PlaceOrderResponse(Guid correlationId) : base(correlationId) { }
    public PlaceOrderResponse() { }

    public int OrderId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal RoundUpAmount { get; set; }
}

/// <summary>
/// Places an order from catalog items. eShopOnWeb has no separate payment step, so a placed order
/// is treated as paid: for an enrolled shopper this sets aside the round-up to the next whole euro.
/// Setting aside change never causes the order to fail.
/// </summary>
public class PlaceOrderEndpoint : IEndpoint<IResult, PlaceOrderRequest, IOrderPlacementService, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PlaceOrderRequest request, IOrderPlacementService orderPlacement, IInvestingService investing, HttpContext httpContext) =>
            {
                request.BuyerId = httpContext.User.Identity?.Name ?? string.Empty;
                return await HandleAsync(request, orderPlacement, investing);
            })
            .Produces<PlaceOrderResponse>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(PlaceOrderRequest request, IOrderPlacementService orderPlacement, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }
        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest("An order must contain at least one item.");
        }

        var lines = request.Items.Select(i => new OrderLine(i.CatalogItemId, i.Quantity)).ToList();

        OrderPlacementResult placement;
        try
        {
            placement = await orderPlacement.PlaceOrderAsync(request.BuyerId, lines, default);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }

        // Best-effort: set aside the change. This never throws and never affects the placed order.
        var roundUp = await investing.RecordPaidOrderAsync(request.BuyerId, placement.Total, default);

        var response = new PlaceOrderResponse(request.CorrelationId())
        {
            OrderId = placement.OrderId,
            RoundUpAmount = roundUp
        };
        return Results.Ok(response);
    }
}
