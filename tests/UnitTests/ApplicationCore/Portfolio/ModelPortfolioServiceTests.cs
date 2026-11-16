using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Portfolio;

public class ModelPortfolioServiceTests
{
    private readonly IRepository<ModelPortfolio> _models = Substitute.For<IRepository<ModelPortfolio>>();
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IUpvestInvestmentGateway _gateway = Substitute.For<IUpvestInvestmentGateway>();
    private readonly IAppLogger<ModelPortfolioService> _logger = Substitute.For<IAppLogger<ModelPortfolioService>>();

    private ModelPortfolioService Service() => new(_models, _investors, _gateway, _logger);

    private static List<FundWeight> Funds(params (string isin, decimal w)[] funds) =>
        funds.Select(f => new FundWeight(f.isin, f.w)).ToList();

    [Fact]
    public async Task RejectsWhenWeightsDoNotSumTo100()
    {
        var ex = await Assert.ThrowsAsync<PortfolioValidationException>(() =>
            Service().UpsertModelAsync("m1", "Model", Funds(("DE0001", 70m), ("DE0002", 20m)), CancellationToken.None));
        Assert.Contains("100", ex.Message);
    }

    [Fact]
    public async Task RejectsDuplicateIsin()
    {
        await Assert.ThrowsAsync<PortfolioValidationException>(() =>
            Service().UpsertModelAsync("m1", "Model", Funds(("DE0001", 50m), ("DE0001", 50m)), CancellationToken.None));
    }

    [Fact]
    public async Task RejectsUnknownIsinWhenInstrumentListAvailable()
    {
        _gateway.ListTradeableIsinsAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { "DE0001" } as IReadOnlySet<string>);

        await Assert.ThrowsAsync<PortfolioValidationException>(() =>
            Service().UpsertModelAsync("m1", "Model", Funds(("DE0001", 50m), ("DE9999", 50m)), CancellationToken.None));
    }

    [Fact]
    public async Task CreatesAllocationAndPersistsModelOnHappyPath()
    {
        _gateway.ListTradeableIsinsAsync(Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { "DE0001", "DE0002" } as IReadOnlySet<string>);
        var allocationId = System.Guid.NewGuid();
        _gateway.CreateModelAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<FundWeight>>(), Arg.Any<System.Guid>(), Arg.Any<CancellationToken>())
            .Returns(allocationId);
        _models.FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<ModelPortfolio>>(), Arg.Any<CancellationToken>())
            .Returns((ModelPortfolio?)null);
        _models.AddAsync(Arg.Any<ModelPortfolio>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<ModelPortfolio>());

        var view = await Service().UpsertModelAsync("balanced", "Balanced", Funds(("DE0001", 60m), ("DE0002", 40m)), CancellationToken.None);

        Assert.Equal("balanced", view.ModelId);
        Assert.Equal(2, view.Funds.Count);
        await _gateway.Received(1).CreateModelAllocationAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<FundWeight>>(), Arg.Any<System.Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChooseModelRefusesWhenAccountNotReady()
    {
        var investor = new Investor("shopper@example.com"); // pending, no account
        _investors.FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns(investor);
        var model = new ModelPortfolio("balanced", "Balanced", Funds(("DE0001", 100m)).Select(f => new ModelFund(f.InstrumentId, f.Weight)));
        model.SetAllocationId(System.Guid.NewGuid());
        _models.FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<ModelPortfolio>>(), Arg.Any<CancellationToken>())
            .Returns(model);

        await Assert.ThrowsAsync<PortfolioStateException>(() =>
            Service().ChooseModelAsync("shopper@example.com", "balanced", CancellationToken.None));
    }
}
