using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.PublicApi.Investing;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class InvestmentsResponse : BaseResponse
{
    public List<InvestmentDto> Investments { get; set; } = new();
}

public class InvestmentDto
{
    public Guid InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    /// <summary>pending | settled | failed.</summary>
    public string Status { get; set; } = string.Empty;
}
