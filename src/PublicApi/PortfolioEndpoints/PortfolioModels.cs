using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public class HoldingDto
{
    /// <summary>The fund's ISIN.</summary>
    public string InstrumentId { get; set; } = string.Empty;
    /// <summary>Quantity of the fund held.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Current value of the holding, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Value { get; set; }
}

public class PortfolioResponse : BaseResponse
{
    public PortfolioResponse(Guid correlationId) : base(correlationId) { }
    public PortfolioResponse() { }

    /// <summary>The model the portfolio follows (null if none chosen yet).</summary>
    public string? ModelId { get; set; }

    public List<HoldingDto> Holdings { get; set; } = new();

    /// <summary>Total current value of the portfolio, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal TotalValue { get; set; }

    /// <summary>Total amount paid in, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PaidIn { get; set; }

    public string? Detail { get; set; }
}
