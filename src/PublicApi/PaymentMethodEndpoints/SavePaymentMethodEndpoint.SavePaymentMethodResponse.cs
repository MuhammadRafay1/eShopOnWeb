using System;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SavePaymentMethodResponse : BaseResponse
{
    public SavePaymentMethodResponse(Guid correlationId) : base(correlationId)
    {
    }

    public SavePaymentMethodResponse()
    {
    }

    public int PaymentMethodId { get; set; }

    public string Description { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Last4 { get; set; } = "";
    public string Expiry { get; set; } = "";
}
