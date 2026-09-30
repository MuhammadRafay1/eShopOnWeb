using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public class Payment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Payment() {}

    public Payment(int orderId, string provider, string currencyCode, decimal authorizedAmount, string invoiceReference)
    {
        OrderId = orderId;
        Provider = provider;
        CurrencyCode = currencyCode;
        AuthorizedAmount = authorizedAmount;
        InvoiceReference = invoiceReference;
    }

    public int OrderId { get; private set; }
    public string Provider { get; private set; }
    public string CurrencyCode { get; private set; }
    public decimal AuthorizedAmount { get; private set; }
    public string InvoiceReference { get; private set; }

    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }

    private readonly List<PaymentRefund> _refunds = new();
    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();

    public void SetCreatedOrder(string payPalOrderId)
    {
        PayPalOrderId = payPalOrderId;
    }

    public void SetAuthorization(string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    public void SetCapture(string captureId, string status, decimal capturedAmount, decimal? payPalFee, decimal? netAmount)
    {
        CaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = capturedAmount;
        PayPalFee = payPalFee;
        NetAmount = netAmount;
    }

    public void AddRefund(PaymentRefund refund)
    {
        _refunds.Add(refund);
    }

    public decimal TotalRefunded() => _refunds.Sum(r => r.Amount);

    public decimal RefundableRemaining() => (CapturedAmount ?? 0) - TotalRefunded();
}
