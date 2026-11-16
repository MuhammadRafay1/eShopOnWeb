using System;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

internal static class PortfolioMapping
{
    public static string ToWire(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    public static string ToWire(PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending"
    };

    public static string ToWire(RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending"
    };

    public static InvestorResponse ToResponse(InvestorView view) => new()
    {
        InvestorId = view.InvestorId,
        Status = ToWire(view.Status)
    };

    public static ModelResponse ToResponse(ModelPortfolioView view) => new()
    {
        ModelId = view.ModelId,
        Name = view.Name,
        Funds = view.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
    };

    public static PaymentResponse ToResponse(PaymentView view) => new()
    {
        PaymentId = view.PaymentId,
        Amount = Round(view.Amount),
        Status = ToWire(view.Status)
    };

    public static RealignmentResponse ToResponse(RealignmentView view) => new()
    {
        RealignmentId = view.RealignmentId,
        Status = ToWire(view.Status)
    };

    public static PortfolioResponse ToResponse(PortfolioView view) => new()
    {
        ModelId = view.ModelId,
        Holdings = view.Holdings.Select(h => new HoldingDto
        {
            InstrumentId = h.InstrumentId,
            Quantity = h.Quantity,
            Value = Round(h.Value)
        }).ToList(),
        TotalValue = Round(view.TotalValue),
        PaidIn = Round(view.PaidIn)
    };

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
