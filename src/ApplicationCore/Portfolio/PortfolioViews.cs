using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

// Application-level views returned by IPortfolioService. The field names required by the task are
// fixed here; the API layer maps these onto its response DTOs. Status strings are the task's
// lower-case vocabulary.

public sealed record InvestorView(Guid InvestorId, string Status);

public sealed record ModelFundView(string InstrumentId, decimal Weight);

public sealed record ModelView(string ModelId, string Name, IReadOnlyList<ModelFundView> Funds);

public sealed record PaymentView(Guid PaymentId, decimal Amount, string Status);

public sealed record RealignmentView(Guid RealignmentId, string Status);

public sealed record HoldingView(string InstrumentId, decimal Quantity, decimal Value);

public sealed record PortfolioView(
    string? ModelId,
    IReadOnlyList<HoldingView> Holdings,
    decimal TotalValue,
    decimal PaidIn);
