using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

/// <summary>Thrown for a caller/application-level rejection (bad input, missing prerequisite,
/// not found). Maps to a 4xx; distinct from a provider failure.</summary>
public sealed class PortfolioValidationException : Exception
{
    public int StatusCode { get; }

    public PortfolioValidationException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed record InvestorResult(Guid InvestorId, InvestorState State);

public sealed record ModelResult(string ModelId, string Name, IReadOnlyList<UpvestFund> Funds);

public sealed record PaymentResult(Guid PaymentId, decimal Amount, PaymentState State);

public sealed record RealignmentResult(Guid RealignmentId, RealignmentState State);

public sealed record PortfolioResult(string? ModelId, IReadOnlyList<UpvestHolding> Holdings, decimal TotalValue, decimal PaidIn);
