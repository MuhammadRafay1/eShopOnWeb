using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

/// <summary>
/// Orchestrates the PayPal payment flows over the existing order model. Holds the state
/// machine (idempotency short-circuits, over-refund guard, stale-hold reauthorization)
/// and translates domain outcomes into either result records or domain exceptions that
/// the endpoints map to HTTP status codes. All PayPal calls go through <see cref="IPayPalClient"/>.
/// </summary>
public class PaymentService : IPaymentService
{
    // Deterministic invoice id reused on the authorize and the capture, and used to line
    // PayPal transactions back up against eShop orders in reconciliation. It embeds the
    // payment's idempotency key (generated once, persisted before the first PayPal call) so
    // it is stable across retries yet globally unique — the sandbox account requires unique
    // invoice ids, and this environment's in-memory DB restarts order ids from 1 each run.
    // Reconciliation recomputes it from the persisted Payment row, so matching still works.
    private const string InvoicePrefix = "eshop-order-";
    private static string InvoiceIdFor(Payment payment) => $"{InvoicePrefix}{payment.OrderId}-{payment.IdempotencyKey:N}";

    // Small tolerance for decimal comparisons on money.
    private const decimal Epsilon = 0.001m;

    private readonly IRepository<Order> _orders;
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<PaymentMethod> _paymentMethods;
    private readonly IRepository<CatalogItem> _catalogItems;
    private readonly IPayPalClient _paypal;
    private readonly IUriComposer _uriComposer;
    private readonly IAppLogger<PaymentService> _logger;
    private readonly string _currency;

    public PaymentService(
        IRepository<Order> orders,
        IRepository<Payment> payments,
        IRepository<PaymentMethod> paymentMethods,
        IRepository<CatalogItem> catalogItems,
        IPayPalClient paypal,
        IUriComposer uriComposer,
        IOptions<PayPalOptions> options,
        IAppLogger<PaymentService> logger)
    {
        _orders = orders;
        _payments = payments;
        _paymentMethods = paymentMethods;
        _catalogItems = catalogItems;
        _paypal = paypal;
        _uriComposer = uriComposer;
        _logger = logger;
        _currency = string.IsNullOrWhiteSpace(options.Value.Currency) ? "USD" : options.Value.Currency.Trim().ToUpperInvariant();
    }

    // ------------------------------------------------------------------ place order

    public async Task<PlaceOrderResult> PlaceOrderAsync(string buyerId, IReadOnlyList<PlaceOrderLine> lines, PaymentAddressInput shipTo, CancellationToken ct)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new BadRequestException("An order must contain at least one item.");
        }
        if (lines.Any(l => l.Quantity <= 0))
        {
            throw new BadRequestException("Every item must have a quantity of at least 1.");
        }

        var ids = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItems.ListAsync(new CatalogItemsSpecification(ids), ct);
        var byId = catalogItems.ToDictionary(c => c.Id);

        var items = new List<OrderItem>();
        foreach (var line in lines)
        {
            if (!byId.TryGetValue(line.CatalogItemId, out var catalogItem))
            {
                throw new BadRequestException($"Catalog item {line.CatalogItemId} does not exist.");
            }
            // Price comes from the catalog, never from the caller.
            var ordered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            items.Add(new OrderItem(ordered, catalogItem.Price, line.Quantity));
        }

        var address = new Address(
            shipTo?.Street ?? "N/A",
            shipTo?.City ?? "N/A",
            shipTo?.State ?? string.Empty,
            shipTo?.Country ?? "N/A",
            shipTo?.ZipCode ?? "00000");

        var order = new Order(buyerId, address, items);
        await _orders.AddAsync(order, ct);

        return new PlaceOrderResult(order.Id, order.Total(), _currency);
    }

    // ------------------------------------------------------------------ pay (authorize)

    public async Task<PayResult> PayOrderAsync(string buyerId, int orderId, CardInput? card, int? paymentMethodId, CancellationToken ct)
    {
        if ((card is null) == (paymentMethodId is null))
        {
            throw new BadRequestException("Provide exactly one of 'card' or 'paymentMethodId'.");
        }

        // Load with items so Order.Total() (the amount we authorize) is correct.
        var order = await _orders.FirstOrDefaultAsync(new OrderWithItemsByIdSpecification(orderId), ct);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new NotFoundException($"Order {orderId} was not found.");
        }

        if (order.Status is OrderStatus.Fulfilled or OrderStatus.Cancelled
            or OrderStatus.PartiallyRefunded or OrderStatus.Refunded)
        {
            throw new ConflictException($"Order {orderId} is {order.Status} and can no longer be paid.");
        }

        var payment = await _payments.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(orderId), ct);

        // Repeat call after a successful authorization: return what we already have, no PayPal call.
        if (order.Status == OrderStatus.PaymentAuthorized && payment is { IsAuthorized: true })
        {
            return ToPayResult(order, payment, alreadyAuthorized: true);
        }

        // AwaitingPayment (or a prior attempt that never completed). Persist the payment row
        // (with its idempotency key) before calling PayPal, so a crash mid-flight leaves a row to retry.
        if (payment is null)
        {
            payment = new Payment(orderId, order.Total(), _currency);
            await _payments.AddAsync(payment, ct);
        }

        // Resolve the payment source.
        string? vaultId = null;
        PayPalCardDetails? cardDetails = null;
        if (paymentMethodId is not null)
        {
            var method = await _paymentMethods.GetByIdAsync(paymentMethodId.Value, ct);
            if (method is null || method.BuyerId != buyerId)
            {
                throw new NotFoundException($"Payment method {paymentMethodId} was not found.");
            }
            vaultId = method.PayPalPaymentTokenId;
        }
        else
        {
            cardDetails = BuildCardDetails(card!);
        }

        var invoiceId = InvoiceIdFor(payment);
        var request = new PayPalAuthorizeOrderRequest
        {
            Amount = payment.Amount,
            Currency = payment.CurrencyCode,
            InvoiceId = invoiceId,
            CustomId = invoiceId,
            RequestId = payment.IdempotencyKey.ToString("N"),
            Card = cardDetails,
            VaultId = vaultId
        };

        var auth = await _paypal.AuthorizeOrderAsync(request, ct);

        payment.SetAuthorization(auth.PayPalOrderId, auth.AuthorizationId, auth.Status, auth.ExpiresAt);
        order.MarkPaymentAuthorized();
        await _payments.UpdateAsync(payment, ct);
        await _orders.UpdateAsync(order, ct);

        return ToPayResult(order, payment, alreadyAuthorized: false);
    }

    // ------------------------------------------------------------------ fulfil (capture)

    public async Task<FulfilResult> FulfilOrderAsync(int orderId, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            throw new NotFoundException($"Order {orderId} was not found.");
        }

        var payment = await _payments.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(orderId), ct);

        if (order.Status == OrderStatus.Fulfilled)
        {
            if (payment?.CaptureId is null)
            {
                throw new ConflictException($"Order {orderId} is marked fulfilled but has no capture on record.");
            }
            return ToFulfilResult(order, payment, reauthorized: false, alreadyFulfilled: true);
        }

        if (order.Status != OrderStatus.PaymentAuthorized || payment is not { IsAuthorized: true })
        {
            throw new ConflictException($"Order {orderId} is {order.Status}; only an authorized order can be fulfilled.");
        }

        // Read the live authorization state — don't trust a possibly-stale local copy.
        var live = await _paypal.GetAuthorizationAsync(payment.AuthorizationId!, ct);
        payment.UpdateAuthorization(live.Id, live.Status, live.ExpiresAt);

        bool reauthorized = false;
        var status = live.Status?.ToUpperInvariant();

        if (status is "VOIDED" or "DENIED")
        {
            throw new UnprocessableEntityException(
                $"The authorization for order {orderId} is {live.Status} and cannot be captured. " +
                "Cancel this order and have the shopper place and pay for a new one.");
        }

        // Stale hold: past its expiry. Renew it before capturing.
        bool stale = live.ExpiresAt.HasValue && DateTimeOffset.UtcNow >= live.ExpiresAt.Value;
        if (stale)
        {
            try
            {
                var reauth = await _paypal.ReauthorizeAsync(
                    payment.AuthorizationId!, payment.Amount, payment.CurrencyCode,
                    payment.IdempotencyKey.ToString("N") + "-reauth", ct);
                payment.UpdateAuthorization(reauth.AuthorizationId, reauth.Status, reauth.ExpiresAt);
                reauthorized = true;
            }
            catch (PayPalApiException ex)
            {
                _logger.LogWarning($"Reauthorization failed for order {orderId} (debug_id={ex.DebugId}): {ex.DescribeIssues()}");
                throw new UnprocessableEntityException(
                    $"The authorization hold for order {orderId} has expired and can no longer be renewed " +
                    $"({ex.DescribeIssues()}). Cancel this order and have the shopper place and pay for a new one.");
            }
        }

        var capture = await _paypal.CaptureAsync(
            payment.AuthorizationId!, payment.Amount, payment.CurrencyCode,
            InvoiceIdFor(payment), payment.IdempotencyKey.ToString("N") + "-capture", ct);

        payment.SetCapture(capture.CaptureId, capture.Status, capture.GrossAmount, capture.PayPalFee, capture.NetAmount);
        order.MarkFulfilled();
        await _payments.UpdateAsync(payment, ct);
        await _orders.UpdateAsync(order, ct);

        return ToFulfilResult(order, payment, reauthorized, alreadyFulfilled: false);
    }

    // ------------------------------------------------------------------ cancel (void)

    public async Task<CancelResult> CancelOrderAsync(int orderId, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            throw new NotFoundException($"Order {orderId} was not found.");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return new CancelResult(orderId, order.Status.ToString(), AuthorizationVoided: false, AlreadyCancelled: true);
        }

        if (order.Status is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded or OrderStatus.Refunded)
        {
            throw new ConflictException(
                $"Order {orderId} is {order.Status}; money has already moved. Use a refund, not a cancel.");
        }

        bool voided = false;
        if (order.Status == OrderStatus.PaymentAuthorized)
        {
            var payment = await _payments.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(orderId), ct);
            if (payment is { IsAuthorized: true })
            {
                await _paypal.VoidAuthorizationAsync(payment.AuthorizationId!, ct);
                payment.UpdateAuthorization(payment.AuthorizationId!, "VOIDED", payment.AuthorizationExpiresAt);
                await _payments.UpdateAsync(payment, ct);
                voided = true;
            }
        }

        order.MarkCancelled();
        await _orders.UpdateAsync(order, ct);

        return new CancelResult(orderId, order.Status.ToString(), voided, AlreadyCancelled: false);
    }

    // ------------------------------------------------------------------ refund

    public async Task<OrderRefundResult> RefundOrderAsync(string buyerId, int orderId, decimal? amount, string idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BadRequestException("An idempotencyKey is required for refunds.");
        }
        if (amount is <= 0)
        {
            throw new BadRequestException("Refund amount, when supplied, must be greater than zero.");
        }

        var order = await _orders.GetByIdAsync(orderId, ct);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new NotFoundException($"Order {orderId} was not found.");
        }

        if (order.Status is not (OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded))
        {
            throw new ConflictException($"Order {orderId} is {order.Status}; only a fulfilled order can be refunded.");
        }

        var payment = await _payments.FirstOrDefaultAsync(new PaymentByOrderIdSpecification(orderId), ct);
        if (payment?.CaptureId is null || payment.CapturedAmount is null)
        {
            throw new ConflictException($"Order {orderId} has no captured payment to refund.");
        }

        // Idempotency: a repeat under the same key returns the existing refund, no PayPal call.
        var existing = payment.FindRefundByIdempotencyKey(idempotencyKey);
        if (existing is not null)
        {
            return new OrderRefundResult(existing.PayPalRefundId, existing.Status, existing.Amount,
                payment.TotalRefunded(), order.Status.ToString(), AlreadyProcessed: true);
        }

        var remaining = payment.RefundableRemaining();
        var requested = amount ?? remaining;
        if (remaining <= Epsilon)
        {
            throw new UnprocessableEntityException($"Order {orderId} has already been fully refunded.");
        }
        if (requested - remaining > Epsilon)
        {
            throw new UnprocessableEntityException(
                $"Refund of {requested:0.00} {payment.CurrencyCode} exceeds the {remaining:0.00} still refundable against this capture.");
        }

        var requestId = $"{payment.IdempotencyKey:N}-refund-{idempotencyKey}";
        var result = await _paypal.RefundAsync(payment.CaptureId!, amount, payment.CurrencyCode, requestId, ct);

        var actualAmount = result.Amount > 0 ? result.Amount : requested;
        payment.AddRefund(result.RefundId, actualAmount, result.Status, idempotencyKey);

        var totalRefunded = result.TotalRefunded ?? payment.TotalRefunded();
        if (payment.CapturedAmount.Value - totalRefunded <= Epsilon)
        {
            order.MarkRefunded();
        }
        else
        {
            order.MarkPartiallyRefunded();
        }

        await _payments.UpdateAsync(payment, ct);
        await _orders.UpdateAsync(order, ct);

        return new OrderRefundResult(result.RefundId, result.Status, actualAmount, totalRefunded,
            order.Status.ToString(), AlreadyProcessed: false);
    }

    // ------------------------------------------------------------------ my orders

    public async Task<IReadOnlyList<OrderSummary>> GetOrdersForBuyerAsync(string buyerId, CancellationToken ct)
    {
        var orders = await _orders.ListAsync(new CustomerOrdersWithItemsSpecification(buyerId), ct);
        var orderIds = orders.Select(o => o.Id).ToArray();
        var payments = orderIds.Length == 0
            ? new List<Payment>()
            : (await _payments.ListAsync(new PaymentsByOrderIdsSpecification(orderIds), ct)).ToList();
        var paymentByOrder = payments.ToDictionary(p => p.OrderId);

        return orders.Select(o =>
        {
            paymentByOrder.TryGetValue(o.Id, out var p);
            return new OrderSummary(o.Id, o.OrderDate, o.Status.ToString(), o.Total(), _currency, ToPaymentSummary(p));
        }).ToList();
    }

    // ------------------------------------------------------------------ saved cards

    public async Task<SavedCardResult> SaveCardAsync(string buyerId, CardInput card, CancellationToken ct)
    {
        var details = BuildCardDetails(card);
        var token = await _paypal.CreateVaultedCardAsync(details, buyerId, ct);

        var method = new PaymentMethod(buyerId, token.Id, token.Brand, token.LastDigits, token.Expiry);
        await _paymentMethods.AddAsync(method, ct);

        return new SavedCardResult(method.Id, method.CardBrand, method.LastDigits, method.ExpiryMonthYear, method.CreatedAt);
    }

    public async Task<IReadOnlyList<SavedCardResult>> ListCardsAsync(string buyerId, CancellationToken ct)
    {
        var methods = await _paymentMethods.ListAsync(new PaymentMethodsByBuyerSpecification(buyerId), ct);
        return methods
            .Select(m => new SavedCardResult(m.Id, m.CardBrand, m.LastDigits, m.ExpiryMonthYear, m.CreatedAt))
            .ToList();
    }

    public async Task DeleteCardAsync(string buyerId, int paymentMethodId, CancellationToken ct)
    {
        var method = await _paymentMethods.GetByIdAsync(paymentMethodId, ct);
        if (method is null || method.BuyerId != buyerId)
        {
            throw new NotFoundException($"Payment method {paymentMethodId} was not found.");
        }

        try
        {
            await _paypal.DeleteVaultedCardAsync(method.PayPalPaymentTokenId, ct);
        }
        catch (PayPalApiException ex) when (ex.HttpStatusCode == 404)
        {
            // Already gone on PayPal's side — proceed to remove the local record.
            _logger.LogWarning($"Vault token for payment method {paymentMethodId} was already absent on PayPal (debug_id={ex.DebugId}).");
        }

        await _paymentMethods.DeleteAsync(method, ct);
    }

    // ------------------------------------------------------------------ reconciliation

    public async Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (to <= from)
        {
            throw new BadRequestException("'to' must be later than 'from'.");
        }

        // Walk the whole range in <=31-day chunks (PayPal's hard limit), deduping by transaction id.
        var scanned = new Dictionary<string, PayPalTransaction>();
        var chunkStart = from;
        var maxChunk = TimeSpan.FromDays(30);
        const int pageSize = 100;

        while (chunkStart < to)
        {
            var chunkEnd = chunkStart + maxChunk;
            if (chunkEnd > to) chunkEnd = to;

            int page = 1;
            while (true)
            {
                var result = await _paypal.SearchTransactionsAsync(chunkStart, chunkEnd, page, pageSize, ct);
                foreach (var txn in result.Transactions)
                {
                    if (!string.IsNullOrEmpty(txn.TransactionId))
                    {
                        scanned[txn.TransactionId] = txn;
                    }
                }

                bool more = result.TotalPages.HasValue
                    ? page < result.TotalPages.Value
                    : result.Transactions.Count >= pageSize;
                if (!more || page >= 200) break; // 200-page safety valve
                page++;
            }

            chunkStart = chunkEnd;
        }

        // eShop-side captured payments in the window.
        var eshopPayments = await _payments.ListAsync(new PaymentsCapturedBetweenSpecification(from, to), ct);
        var eshopByInvoice = eshopPayments.ToDictionary(p => InvoiceIdFor(p));

        var matched = new List<MatchedReconItem>();
        var payPalOnly = new List<PayPalOnlyReconItem>();
        var seenInvoices = new HashSet<string>();

        // Only consider PayPal transactions that carry our invoice scheme; unrelated account
        // activity in the same window is irrelevant to this reconciliation.
        foreach (var txn in scanned.Values.Where(t => (t.InvoiceId ?? t.CustomField)?.StartsWith(InvoicePrefix, StringComparison.Ordinal) == true))
        {
            var invoiceId = txn.InvoiceId ?? txn.CustomField!;
            if (eshopByInvoice.TryGetValue(invoiceId, out var payment))
            {
                seenInvoices.Add(invoiceId);
                matched.Add(new MatchedReconItem(payment.OrderId, invoiceId, txn.TransactionId,
                    payment.CapturedAmount ?? payment.Amount, txn.Amount, txn.Status));
            }
            else
            {
                payPalOnly.Add(new PayPalOnlyReconItem(txn.TransactionId, txn.InvoiceId, txn.Amount, txn.Status, txn.InitiationDate));
            }
        }

        var eShopOnly = eshopByInvoice
            .Where(kv => !seenInvoices.Contains(kv.Key))
            .Select(kv => new EShopOnlyReconItem(kv.Value.OrderId, kv.Key, kv.Value.CapturedAmount ?? kv.Value.Amount, kv.Value.CapturedAt))
            .ToList();

        return new ReconciliationReport(
            from, to, scanned.Count,
            "Payment.CapturedAt (UTC)",
            matched, payPalOnly, eShopOnly);
    }

    // ------------------------------------------------------------------ helpers

    private static PayPalCardDetails BuildCardDetails(CardInput card)
    {
        if (string.IsNullOrWhiteSpace(card.Number))
        {
            throw new BadRequestException("A card number is required.");
        }
        if (card.ExpiryMonth is < 1 or > 12)
        {
            throw new BadRequestException("Card expiry month must be between 1 and 12.");
        }
        if (card.ExpiryYear < 2000 || card.ExpiryYear > 2100)
        {
            throw new BadRequestException("Card expiry year is invalid.");
        }

        var expiry = string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}", card.ExpiryYear, card.ExpiryMonth);
        PayPalBillingAddress? billing = null;
        if (card.BillingAddress is not null)
        {
            var b = card.BillingAddress;
            billing = new PayPalBillingAddress
            {
                AddressLine1 = b.Street,
                AdminArea2 = b.City,
                AdminArea1 = b.State,
                PostalCode = b.ZipCode,
                CountryCode = string.IsNullOrWhiteSpace(b.Country) ? "US" : b.Country!
            };
        }

        return new PayPalCardDetails
        {
            Number = card.Number.Replace(" ", string.Empty),
            Expiry = expiry,
            SecurityCode = card.SecurityCode,
            CardholderName = card.CardholderName,
            BillingAddress = billing
        };
    }

    private PayResult ToPayResult(Order order, Payment payment, bool alreadyAuthorized) =>
        new(order.Id, order.Status.ToString(), payment.AuthorizationId ?? string.Empty,
            payment.AuthorizationStatus ?? string.Empty, payment.AuthorizationExpiresAt,
            payment.Amount, payment.CurrencyCode, alreadyAuthorized);

    private FulfilResult ToFulfilResult(Order order, Payment payment, bool reauthorized, bool alreadyFulfilled) =>
        new(order.Id, order.Status.ToString(), payment.CaptureId ?? string.Empty,
            payment.CaptureStatus ?? string.Empty, payment.CapturedAmount ?? 0m,
            payment.PayPalFeeAmount, payment.NetAmount, payment.CurrencyCode, reauthorized, alreadyFulfilled);

    private static PaymentSummary? ToPaymentSummary(Payment? p)
    {
        if (p is null) return null;
        var refunds = p.Refunds
            .Select(r => new RefundSummary(r.PayPalRefundId, r.Amount, r.Status, r.CreatedAt))
            .ToList();
        return new PaymentSummary(
            p.AuthorizationId, p.AuthorizationStatus, p.AuthorizationExpiresAt,
            p.CaptureId, p.CaptureStatus, p.CapturedAmount, p.PayPalFeeAmount, p.NetAmount,
            p.TotalRefunded(), refunds);
    }
}
