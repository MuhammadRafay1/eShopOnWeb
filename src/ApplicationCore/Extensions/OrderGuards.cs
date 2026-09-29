using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Ardalis.GuardClauses;

public static class OrderGuards
{
    /// <summary>
    /// Guards an order lifecycle transition: throws <see cref="InvalidOrderStateException"/>
    /// unless the order is currently in the required state.
    /// </summary>
    public static void InvalidOrderStateTransition(this IGuardClause guardClause,
        OrderStatus current, OrderStatus required, string transition)
    {
        if (current != required)
        {
            throw new InvalidOrderStateException(
                $"Cannot {transition}: order is {current}, but must be {required}.");
        }
    }
}
