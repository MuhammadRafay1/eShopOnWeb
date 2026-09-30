using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentService : IPaymentService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<SavedPaymentMethod> _savedCardRepository;
    private readonly IPayPalGateway _payPal;
    private readonly IOrderLockProvider _lockProvider;
    private readonly PayPalSettings _settings;
    private readonly IAppLogger<PaymentService> _logger;

    public PaymentService(
        IRepository<Order> orderRepository,
        IRepository<SavedPaymentMethod> savedCardRepository,
        IPayPalGateway payPal,
        IOrderLockProvider lockProvider,
        IOptions<PayPalSettings> settings,
        IAppLogger<PaymentService> logger)
    {
        _orderRepository = orderRepository;
        _savedCardRepository = savedCardRepository;
        _payPal = payPal;
        _lockProvider = lockProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<Order>> AuthorizeAsync(int orderId, string buyerId, PaymentInstrument instrument)
    {
        var hasCard = instrument.Card is not null;
        var hasSavedCard = instrument.SavedPaymentMethodId.HasValue;
        if (hasCard == hasSavedCard)
        {
            return Result<Order>.Invalid(new List<ValidationError>
            {
                new() { Identifier = "payment", ErrorMessage = "Provide exactly one of a card or a savedPaymentMethodId." }
            });
        }

        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
        if (order is null || order.BuyerId != buyerId)
        {
            return Result<Order>.NotFound();
        }

        using (await _lockProvider.AcquireAsync(orderId))
        {
            // Re-read state under the lock: another request may have authorized this order already.
            order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
            if (order is null || order.BuyerId != buyerId)
            {
                return Result<Order>.NotFound();
            }

            if (order.IsAuthorized || order.Status != OrderStatus.AwaitingPayment)
            {
                // Idempotent: a double-click (or a retry) never authorizes twice.
                return Result<Order>.Success(order);
            }

            string? vaultId = null;
            if (hasSavedCard)
            {
                var savedCard = await _savedCardRepository.FirstOrDefaultAsync(
                    new SavedCardByIdAndBuyerSpecification(instrument.SavedPaymentMethodId!.Value, buyerId));
                if (savedCard is null)
                {
                    return Result<Order>.Invalid(new List<ValidationError>
                    {
                        new() { Identifier = "savedPaymentMethodId", ErrorMessage = "Saved card not found." }
                    });
                }
                vaultId = savedCard.PayPalVaultId;
            }

            var currency = _settings.Currency!;
            var authorizeRequest = new PayPalAuthorizeRequest(
                EshopOrderId: order.Id.ToString(),
                Amount: order.Total(),
                Currency: currency,
                Card: instrument.Card,
                VaultId: vaultId,
                IdempotencyKey: $"auth-{order.Id}");

            var result = await _payPal.AuthorizeAsync(authorizeRequest);

            order.MarkAuthorized(currency, result.PayPalOrderId, result.AuthorizationId, result.Status, result.ExpiresAt);
            await _orderRepository.UpdateAsync(order);

            return Result<Order>.Success(order);
        }
    }

    public async Task<Result<Order>> FulfilAsync(int orderId)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
        if (order is null)
        {
            return Result<Order>.NotFound();
        }

        using (await _lockProvider.AcquireAsync(orderId))
        {
            order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
            if (order is null)
            {
                return Result<Order>.NotFound();
            }

            if (order.Status == OrderStatus.Fulfilled)
            {
                // Idempotent: already captured, don't capture again.
                return Result<Order>.Success(order);
            }

            if (order.Status != OrderStatus.Authorized || order.Payment?.AuthorizationId is null)
            {
                return Result<Order>.Invalid(new List<ValidationError>
                {
                    new() { Identifier = "orderId", ErrorMessage = $"Order {orderId} is not awaiting fulfilment (status: {order.Status})." }
                });
            }

            var authorizationId = order.Payment.AuthorizationId;
            PayPalCaptureResult capture;
            try
            {
                capture = await _payPal.CaptureAsync(authorizationId, $"capture-{authorizationId}");
            }
            catch (PayPalAuthorizationStaleException staleEx)
            {
                _logger.LogWarning("Authorization {0} for order {1} could not be captured directly ({2}); attempting reauthorization.", authorizationId, orderId, staleEx.Message);

                try
                {
                    var reauth = await _payPal.ReauthorizeAsync(authorizationId, order.Total(), order.Payment.Currency, $"reauth-{authorizationId}");
                    order.ReplaceAuthorization(reauth.AuthorizationId, reauth.Status, reauth.ExpiresAt);
                    await _orderRepository.UpdateAsync(order);

                    capture = await _payPal.CaptureAsync(reauth.AuthorizationId, $"capture-{reauth.AuthorizationId}");
                }
                catch (PayPalException)
                {
                    throw new PayPalException(
                        $"The payment authorization for order {orderId} has expired and can no longer be renewed. Ask the shopper to pay again before fulfilling.",
                        422);
                }
            }

            order.MarkFulfilled(capture.CaptureId, capture.CapturedAmount, capture.PayPalFee, capture.NetAmount);
            await _orderRepository.UpdateAsync(order);

            return Result<Order>.Success(order);
        }
    }

    public async Task<Result<Order>> CancelAsync(int orderId)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
        if (order is null)
        {
            return Result<Order>.NotFound();
        }

        using (await _lockProvider.AcquireAsync(orderId))
        {
            order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
            if (order is null)
            {
                return Result<Order>.NotFound();
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                return Result<Order>.Success(order); // idempotent
            }

            if (order.Status != OrderStatus.Authorized || order.Payment?.AuthorizationId is null)
            {
                return Result<Order>.Invalid(new List<ValidationError>
                {
                    new() { Identifier = "orderId", ErrorMessage = $"Order {orderId} cannot be cancelled from status {order.Status}. Once fulfilled, use a refund instead." }
                });
            }

            await _payPal.VoidAsync(order.Payment.AuthorizationId, $"void-{order.Payment.AuthorizationId}");

            order.MarkCancelled();
            await _orderRepository.UpdateAsync(order);

            return Result<Order>.Success(order);
        }
    }

    public async Task<Result<RefundOutcome>> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Result<RefundOutcome>.Invalid(new List<ValidationError>
            {
                new() { Identifier = "idempotencyKey", ErrorMessage = "An idempotencyKey is required for refunds." }
            });
        }

        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
        if (order is null || order.BuyerId != buyerId)
        {
            return Result<RefundOutcome>.NotFound();
        }

        using (await _lockProvider.AcquireAsync(orderId))
        {
            order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
            if (order is null || order.BuyerId != buyerId)
            {
                return Result<RefundOutcome>.NotFound();
            }

            var existing = order.FindRefundByKey(idempotencyKey);
            if (existing is not null)
            {
                // Idempotent replay: return the refund already made under this key rather than refunding again.
                return Result<RefundOutcome>.Success(new RefundOutcome(order, existing));
            }

            if (order.Payment?.CaptureId is null)
            {
                return Result<RefundOutcome>.Invalid(new List<ValidationError>
                {
                    new() { Identifier = "orderId", ErrorMessage = $"Order {orderId} has not been fulfilled; nothing to refund." }
                });
            }

            var requestedAmount = amount ?? order.RefundableRemaining();
            if (requestedAmount <= 0 || requestedAmount > order.RefundableRemaining())
            {
                return Result<RefundOutcome>.Invalid(new List<ValidationError>
                {
                    new()
                    {
                        Identifier = "amount",
                        ErrorMessage = $"Refund amount must be positive and at most the remaining refundable amount ({order.RefundableRemaining()})."
                    }
                });
            }

            var result = await _payPal.RefundAsync(order.Payment.CaptureId, requestedAmount, order.Payment.Currency, idempotencyKey);

            var refund = order.AddRefund(result.RefundId, requestedAmount, result.Status, idempotencyKey);
            await _orderRepository.UpdateAsync(order);

            return Result<RefundOutcome>.Success(new RefundOutcome(order, refund));
        }
    }
}
