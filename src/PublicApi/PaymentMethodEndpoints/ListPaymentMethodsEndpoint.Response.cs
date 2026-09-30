using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class ListPaymentMethodsResponse : BaseResponse
{
    public ListPaymentMethodsResponse()
    {
    }

    public List<SavedCardSummary> PaymentMethods { get; set; } = new();
}
