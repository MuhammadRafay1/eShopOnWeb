using System;

namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

public record PayPalAuthorizationResult(
    string PayPalOrderId,
    string AuthorizationId,
    string Status,
    DateTimeOffset? ExpiresAt);
