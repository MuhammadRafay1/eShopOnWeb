namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

public record PayPalCaptureResult(
    string CaptureId,
    string Status,
    decimal CapturedAmount,
    decimal PayPalFee,
    decimal NetAmount);
