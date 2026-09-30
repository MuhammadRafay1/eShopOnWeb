using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IPaymentService
{
    /// <summary>Authorizes (holds) the order's total. <paramref name="buyerId"/> must own the order.</summary>
    Task<Result<Order>> AuthorizeAsync(int orderId, string buyerId, PaymentInstrument instrument);

    /// <summary>Captures the held funds. Operator action - not scoped to a buyer.</summary>
    Task<Result<Order>> FulfilAsync(int orderId);

    /// <summary>Voids the hold before fulfilment. Operator action - not scoped to a buyer.</summary>
    Task<Result<Order>> CancelAsync(int orderId);

    /// <summary>Refunds (in full or in part) a fulfilled order's captured payment. <paramref name="buyerId"/> must own the order.</summary>
    Task<Result<RefundOutcome>> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey);
}

public record RefundOutcome(Order Order, Refund Refund);
