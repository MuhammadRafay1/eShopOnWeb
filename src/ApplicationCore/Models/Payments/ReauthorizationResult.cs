using System;

namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class ReauthorizationResult
{
    public string AuthorizationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
}
