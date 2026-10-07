using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class InvestmentsResponse : BaseResponse
{
    public InvestmentsResponse(Guid correlationId) : base(correlationId) { }

    public InvestmentsResponse() { }

    public List<InvestmentDto> Investments { get; set; } = new();
}

/// <summary>One investment, newest first in the list.</summary>
public class InvestmentDto
{
    public Guid InvestmentId { get; set; }

    /// <summary>Amount invested, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    /// <summary><c>pending</c> until the outcome at Upvest is known, then <c>settled</c> or <c>failed</c>.</summary>
    public string Status { get; set; } = "pending";
}
