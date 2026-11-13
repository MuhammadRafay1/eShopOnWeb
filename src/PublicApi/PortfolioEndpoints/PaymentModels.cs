using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public class CreatePaymentRequest : BaseRequest
{
    /// <summary>Amount to pay in, in euros.</summary>
    public decimal Amount { get; set; }
    /// <summary>Currency; defaults to EUR.</summary>
    public string? Currency { get; set; }
}

public class PaymentResponse : BaseResponse
{
    public PaymentResponse(Guid correlationId) : base(correlationId) { }
    public PaymentResponse() { }

    public int PaymentId { get; set; }
    /// <summary>pending | invested | failed.</summary>
    public string Status { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public class PaymentItemDto
{
    public int PaymentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    /// <summary>pending | invested | failed.</summary>
    public string Status { get; set; } = string.Empty;
}

public class PaymentsResponse : BaseResponse
{
    public PaymentsResponse(Guid correlationId) : base(correlationId) { }
    public PaymentsResponse() { }

    public List<PaymentItemDto> Payments { get; set; } = new();
}
