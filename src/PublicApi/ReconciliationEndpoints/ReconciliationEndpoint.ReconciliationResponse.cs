using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

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
    public List<ReconciliationMatchDto> Matched { get; set; } = new();
    public List<ReconciliationPayPalOnlyDto> PayPalOnly { get; set; } = new();
    public List<ReconciliationEShopOnlyDto> EShopOnly { get; set; } = new();
    public ReconciliationCountsDto Counts { get; set; } = new();

    /// <summary>
    /// PayPal's transaction reporting lags live activity, so a range covering payments just
    /// created may legitimately come back empty - that is expected, not a fault in this report.
    /// </summary>
    public string Note { get; set; } =
        "PayPal transaction reporting can lag live activity; a very recent 'to' may legitimately return fewer or no PayPal-side rows.";
}

public class ReconciliationMatchDto
{
    public int OrderId { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public decimal EShopCapturedAmount { get; set; }
    public decimal? PayPalAmount { get; set; }
    public decimal? PayPalFeeAmount { get; set; }
    public string? PayPalStatus { get; set; }
}

public class ReconciliationPayPalOnlyDto
{
    public string TransactionId { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? InvoiceId { get; set; }
    public string? CustomField { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTimeOffset? InitiationDate { get; set; }
}

public class ReconciliationEShopOnlyDto
{
    public int OrderId { get; set; }
    public string CaptureId { get; set; } = string.Empty;
    public decimal CapturedAmount { get; set; }
}

public class ReconciliationCountsDto
{
    public int Matched { get; set; }
    public int PayPalOnly { get; set; }
    public int EShopOnly { get; set; }
}
