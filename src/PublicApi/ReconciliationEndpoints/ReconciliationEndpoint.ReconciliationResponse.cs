using System;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

public class ReconciliationOrderSideDto
{
    public int OrderId { get; set; }
    public string? PayPalOrderId { get; set; }
    public string? CaptureId { get; set; }
    public decimal? CapturedAmount { get; set; }
    public string? Status { get; set; }
}

public class ReconciliationPayPalSideDto
{
    public string TransactionId { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Status { get; set; }
    public string? InvoiceId { get; set; }
    public string? CustomField { get; set; }
}

public class ReconciliationMatchDto
{
    public ReconciliationOrderSideDto Order { get; set; } = null!;
    public ReconciliationPayPalSideDto PayPal { get; set; } = null!;
}

public class ReconciliationResponse : BaseResponse
{
    public ReconciliationResponse(Guid correlationId) : base(correlationId)
    {
    }

    public ReconciliationResponse()
    {
    }

    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public ReconciliationMatchDto[] Matched { get; set; } = Array.Empty<ReconciliationMatchDto>();
    public ReconciliationPayPalSideDto[] PayPalOnly { get; set; } = Array.Empty<ReconciliationPayPalSideDto>();
    public ReconciliationOrderSideDto[] EShopOnly { get; set; } = Array.Empty<ReconciliationOrderSideDto>();

    /// <summary>False if a safety page cap was hit before PayPal signalled the end of its result set - the report is a partial view of the range, not an error.</summary>
    public bool Complete { get; set; }
}
