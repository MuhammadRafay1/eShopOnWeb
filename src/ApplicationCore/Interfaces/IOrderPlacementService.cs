using System.Collections.Generic;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public record OrderItemRequestLine(int CatalogItemId, int Quantity);

public interface IOrderPlacementService
{
    /// <summary>
    /// Builds and persists a new order in the AwaitingPayment state, priced from current catalog prices
    /// (never from client input).
    /// </summary>
    Task<Result<Order>> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderItemRequestLine> items, Address? shipToAddress);
}
