using System;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class ReconciliationRequest : BaseRequest
{
    public DateTimeOffset From { get; }
    public DateTimeOffset To { get; }

    public ReconciliationRequest(DateTimeOffset from, DateTimeOffset to)
    {
        From = from;
        To = to;
    }
}
