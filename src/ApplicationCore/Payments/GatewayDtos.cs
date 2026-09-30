using System;

namespace Microsoft.eShopWeb.ApplicationCore.Payments;

public record GatewayCreatedOrder(string PayPalOrderId, string Status);

public record GatewayAuthorization(string AuthorizationId, string Status, DateTimeOffset? ExpiresAt);

public record GatewayCapture(string CaptureId, string Status, decimal GrossAmount, decimal? PayPalFee, decimal? NetAmount, string Currency);

public record GatewayRefund(string RefundId, string Status, decimal Amount);

public record GatewaySavedCard(string VaultId, string Brand, string LastDigits, string Expiry, string? CardholderName);

public record GatewayTransaction(string TransactionId, string? Status, decimal Amount, string Currency, string? InvoiceId, DateTimeOffset? InitiationDate);
