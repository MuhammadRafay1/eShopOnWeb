using System;

namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class AuthorizationResult
{
    public string PayPalOrderId { get; set; } = string.Empty;
    public string AuthorizationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal HeldAmount { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
