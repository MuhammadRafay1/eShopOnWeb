using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class PortfolioQueryService : IPortfolioQueryService
{
    private readonly IRepository<Investor> _investors;
    private readonly IReadRepository<ModelPortfolio> _models;
    private readonly IReadRepository<PortfolioPayment> _payments;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<PortfolioQueryService> _logger;

    public PortfolioQueryService(
        IRepository<Investor> investors,
        IReadRepository<ModelPortfolio> models,
        IReadRepository<PortfolioPayment> payments,
        IUpvestInvestmentGateway gateway,
        IAppLogger<PortfolioQueryService> logger)
    {
        _investors = investors;
        _models = models;
        _payments = payments;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<PortfolioView?> GetPortfolioAsync(string shopperUserName, CancellationToken ct)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpecification(shopperUserName), ct);
        if (investor is null)
        {
            return null;
        }

        // paidIn: the app's own sum of amounts that reached the account and were invested.
        var payments = await _payments.ListAsync(new PaymentsByUserSpecification(shopperUserName), ct);
        var paidIn = payments.Where(p => p.Status == PaymentStatus.Invested).Sum(p => p.Amount);

        // The model the portfolio follows, by the shop's own modelId.
        string? modelId = await ResolveSelectedModelKeyAsync(investor, ct);

        if (investor.UpvestAccountId is null)
        {
            return new PortfolioView(modelId, Array.Empty<PortfolioHolding>(), 0m, paidIn);
        }

        try
        {
            var snapshot = await _gateway.GetPortfolioAsync(investor.UpvestAccountId.Value, ct);
            // Prefer the model Upvest reports the account follows; fall back to the stored choice.
            if (snapshot.AllocationId is not null)
            {
                var byAllocation = await FindModelKeyByAllocationAsync(snapshot.AllocationId.Value, ct);
                modelId = byAllocation ?? modelId;
            }
            return new PortfolioView(modelId, snapshot.Holdings, snapshot.TotalValue, paidIn);
        }
        catch (UpvestGatewayException ex)
        {
            _logger.LogWarning("Could not read portfolio from Upvest (status {Status}); returning app-side view.", ex.StatusCode);
            return new PortfolioView(modelId, Array.Empty<PortfolioHolding>(), 0m, paidIn);
        }
    }

    private async Task<string?> ResolveSelectedModelKeyAsync(Investor investor, CancellationToken ct)
    {
        if (investor.SelectedModelPortfolioId is null)
        {
            return null;
        }
        var model = await _models.FirstOrDefaultAsync(new ModelPortfolioByIdSpecification(investor.SelectedModelPortfolioId.Value), ct);
        return model?.ModelKey;
    }

    private async Task<string?> FindModelKeyByAllocationAsync(Guid allocationId, CancellationToken ct)
    {
        var models = await _models.ListAsync(new AllModelPortfoliosSpecification(), ct);
        return models.FirstOrDefault(m => m.UpvestAllocationId == allocationId)?.ModelKey;
    }
}
