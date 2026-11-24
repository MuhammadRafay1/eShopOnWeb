using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

internal static class PortfolioResponseMapper
{
    public static InvestorResponse ToResponse(this InvestorView v) =>
        new() { InvestorId = v.InvestorId, Status = v.Status };

    public static ModelResponse ToResponse(this ModelView v) =>
        new()
        {
            ModelId = v.ModelId,
            Name = v.Name,
            Funds = v.Funds.Select(f => new ModelFundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
        };

    public static PaymentResponse ToResponse(this PaymentView v) =>
        new() { PaymentId = v.PaymentId, Amount = v.Amount, Status = v.Status };

    public static RealignmentResponse ToResponse(this RealignmentView v) =>
        new() { RealignmentId = v.RealignmentId, Status = v.Status };

    public static PortfolioResponse ToResponse(this PortfolioView v) =>
        new()
        {
            ModelId = v.ModelId,
            Holdings = v.Holdings.Select(h => new HoldingResponse
            {
                InstrumentId = h.InstrumentId,
                Quantity = h.Quantity,
                Value = h.Value
            }).ToList(),
            TotalValue = v.TotalValue,
            PaidIn = v.PaidIn
        };
}
