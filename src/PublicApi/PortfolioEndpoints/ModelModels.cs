using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public class PutModelRequest : BaseRequest
{
    public string? Name { get; set; }
    public List<FundDto>? Funds { get; set; }
}

public class FundDto
{
    /// <summary>The fund's ISIN.</summary>
    public string? InstrumentId { get; set; }
    /// <summary>Target weight as a percentage; a model's weights add up to exactly 100.</summary>
    public decimal Weight { get; set; }
}

public class ModelDto
{
    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
}

public class ModelResponse : BaseResponse
{
    public ModelResponse(Guid correlationId) : base(correlationId) { }
    public ModelResponse() { }

    public string ModelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FundDto> Funds { get; set; } = new();
    public string? Detail { get; set; }
}

public class ModelsResponse : BaseResponse
{
    public ModelsResponse(Guid correlationId) : base(correlationId) { }
    public ModelsResponse() { }

    public List<ModelDto> Models { get; set; } = new();
}

public class SelectModelRequest : BaseRequest
{
    public string? ModelId { get; set; }
}

public class SelectModelResponse : BaseResponse
{
    public SelectModelResponse(Guid correlationId) : base(correlationId) { }
    public SelectModelResponse() { }

    public string ModelId { get; set; } = string.Empty;
    public string? Detail { get; set; }
}
