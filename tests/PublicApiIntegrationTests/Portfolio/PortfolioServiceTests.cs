using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.PublicApi.Portfolio.Data;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.Portfolio;

[TestClass]
public class PortfolioServiceTests
{
    private static PortfolioContext NewDb() =>
        new(new DbContextOptionsBuilder<PortfolioContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static InvestorSignupCommand SampleSignup() => new(
        "Alex", "Shopper", "alex@example.com", new DateTimeOffset(1990, 5, 15, 0, 0, 0, TimeSpan.Zero),
        "Berlin", "DE", "DE", "Hauptstr 1", "10115", "Berlin", "DE", "491234567890", "DE123", "DE",
        "PASSPORT", "X1", "DE", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static async Task<(ModelPortfolioService svc, string modelId)> SeedModelAsync(PortfolioContext db, FakeUpvestGateway gw)
    {
        var svc = new ModelPortfolioService(db, gw, NullLogger<ModelPortfolioService>.Instance);
        await svc.PutModelAsync("balanced", "Balanced",
            new List<ModelFundInput> { new("US0378331005", 60m), new("US5949181045", 40m) }, CancellationToken.None);
        return (svc, "balanced");
    }

    [TestMethod]
    public async Task SignUp_WithReadyAccount_IsActive()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        var svc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);

        var result = await svc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None);

        Assert.AreEqual("active", result.Status);
        Assert.AreEqual(gw.UserId, result.InvestorId);
    }

    [TestMethod]
    public async Task SignUp_WhenAccountScopesUnauthorized_StaysPendingButCreatesUser()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        gw.UnauthorizedScopes.Add(nameof(FakeUpvestGateway.CreateAccountGroupAsync));
        var svc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);

        var result = await svc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None);

        Assert.AreEqual("pending", result.Status);
        Assert.AreEqual(gw.UserId, result.InvestorId); // user WAS created
    }

    [TestMethod]
    public async Task SignUp_Twice_ReturnsSameInvestor()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        var svc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);

        var first = await svc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None);
        var second = await svc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None);

        Assert.AreEqual(first.InvestorId, second.InvestorId);
        Assert.AreEqual(1, db.Investors.Count());
    }

    [TestMethod]
    public async Task ChooseModel_UnknownModel_Throws404()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        var svc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);
        await svc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None);

        var ex = await Assert.ThrowsExceptionAsync<PortfolioRequestException>(
            () => svc.ChooseModelAsync("shopper1", "nope", CancellationToken.None));
        Assert.AreEqual(404, ex.StatusCode);
    }

    [TestMethod]
    public async Task PutModel_WeightsNotHundred_Throws400()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        var svc = new ModelPortfolioService(db, gw, NullLogger<ModelPortfolioService>.Instance);

        var ex = await Assert.ThrowsExceptionAsync<PortfolioRequestException>(
            () => svc.PutModelAsync("m", "M", new List<ModelFundInput> { new("US0378331005", 50m), new("US5949181045", 40m) }, CancellationToken.None));
        Assert.AreEqual(400, ex.StatusCode);
    }

    [TestMethod]
    public async Task PutModel_ReplacesFundsOnSecondPut()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        var svc = new ModelPortfolioService(db, gw, NullLogger<ModelPortfolioService>.Instance);

        await svc.PutModelAsync("m", "M1", new List<ModelFundInput> { new("US0378331005", 100m) }, CancellationToken.None);
        var replaced = await svc.PutModelAsync("m", "M2", new List<ModelFundInput> { new("US5949181045", 100m) }, CancellationToken.None);

        Assert.AreEqual("M2", replaced.Name);
        Assert.AreEqual(1, replaced.Funds.Count);
        Assert.AreEqual("US5949181045", replaced.Funds[0].InstrumentId);
        Assert.AreEqual(1, db.Models.Count());
    }

    [TestMethod]
    public async Task PayIn_NotActive_Refused()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        gw.UnauthorizedScopes.Add(nameof(FakeUpvestGateway.CreateAccountGroupAsync)); // stays pending
        var investorSvc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);
        await investorSvc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None);
        await SeedModelAsync(db, gw);
        await investorSvc.ChooseModelAsync("shopper1", "balanced", CancellationToken.None);

        var pay = new PaymentService(db, gw, NullLogger<PaymentService>.Instance);
        var ex = await Assert.ThrowsExceptionAsync<PortfolioRequestException>(
            () => pay.PayInAsync("shopper1", 100m, CancellationToken.None));
        Assert.AreEqual(409, ex.StatusCode);
    }

    [TestMethod]
    public async Task PayIn_NoModelChosen_Refused()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        var investorSvc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);
        await investorSvc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None); // active, but no model

        var pay = new PaymentService(db, gw, NullLogger<PaymentService>.Instance);
        var ex = await Assert.ThrowsExceptionAsync<PortfolioRequestException>(
            () => pay.PayInAsync("shopper1", 100m, CancellationToken.None));
        Assert.AreEqual(409, ex.StatusCode);
    }

    [TestMethod]
    public async Task PayIn_HappyPath_InvestedAndListedNewestFirst()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway(); // active account, order FILLED
        var investorSvc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);
        await investorSvc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None);
        await SeedModelAsync(db, gw);
        await investorSvc.ChooseModelAsync("shopper1", "balanced", CancellationToken.None);

        var pay = new PaymentService(db, gw, NullLogger<PaymentService>.Instance);
        var p1 = await pay.PayInAsync("shopper1", 100m, CancellationToken.None);
        var p2 = await pay.PayInAsync("shopper1", 50m, CancellationToken.None);

        Assert.AreEqual("invested", p1.Status);
        var list = await pay.ListPaymentsAsync("shopper1", CancellationToken.None);
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(p2.PaymentId, list[0].PaymentId); // newest first
    }

    [TestMethod]
    public async Task PayIn_IsolatedPerShopper()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        var investorSvc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);
        await investorSvc.SignUpAsync("shopperA", SampleSignup(), CancellationToken.None);
        await SeedModelAsync(db, gw);
        await investorSvc.ChooseModelAsync("shopperA", "balanced", CancellationToken.None);
        var pay = new PaymentService(db, gw, NullLogger<PaymentService>.Instance);
        await pay.PayInAsync("shopperA", 100m, CancellationToken.None);

        var othersView = await pay.ListPaymentsAsync("shopperB", CancellationToken.None);
        Assert.AreEqual(0, othersView.Count); // shopperB never sees shopperA's payment
    }

    [TestMethod]
    public async Task Realignment_TriggerAndStatus()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        await SeedModelAsync(db, gw);
        var svc = new RealignmentService(db, gw, NullLogger<RealignmentService>.Instance);

        var triggered = await svc.TriggerAsync("balanced", CancellationToken.None);
        var status = await svc.GetStatusAsync(triggered.RealignmentId, CancellationToken.None);

        Assert.IsNotNull(status);
        Assert.AreEqual("completed", status!.Status);
    }

    [TestMethod]
    public async Task Realignment_UnknownModel_Throws404()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        var svc = new RealignmentService(db, gw, NullLogger<RealignmentService>.Instance);

        var ex = await Assert.ThrowsExceptionAsync<PortfolioRequestException>(
            () => svc.TriggerAsync("nope", CancellationToken.None));
        Assert.AreEqual(404, ex.StatusCode);
    }

    [TestMethod]
    public async Task Portfolio_ShowsHoldingsModelAndPaidIn()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        var investorSvc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);
        await investorSvc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None);
        await SeedModelAsync(db, gw);
        await investorSvc.ChooseModelAsync("shopper1", "balanced", CancellationToken.None);
        var pay = new PaymentService(db, gw, NullLogger<PaymentService>.Instance);
        await pay.PayInAsync("shopper1", 100m, CancellationToken.None);

        var q = new PortfolioQueryService(db, gw, NullLogger<PortfolioQueryService>.Instance);
        var view = await q.GetPortfolioAsync("shopper1", CancellationToken.None);

        Assert.IsNotNull(view);
        Assert.AreEqual("balanced", view!.ModelId);
        Assert.AreEqual(250.00m, view.TotalValue);
        Assert.AreEqual(2, view.Holdings.Count);
        Assert.AreEqual(100.00m, view.PaidIn);
    }

    [TestMethod]
    public async Task Portfolio_ValuationUnauthorized_StillReturnsModelAndPaidIn()
    {
        using var db = NewDb();
        var gw = new FakeUpvestGateway();
        gw.UnauthorizedScopes.Add(nameof(FakeUpvestGateway.GetValuationAsync));
        var investorSvc = new InvestorService(db, gw, NullLogger<InvestorService>.Instance);
        await investorSvc.SignUpAsync("shopper1", SampleSignup(), CancellationToken.None);
        await SeedModelAsync(db, gw);
        await investorSvc.ChooseModelAsync("shopper1", "balanced", CancellationToken.None);

        var q = new PortfolioQueryService(db, gw, NullLogger<PortfolioQueryService>.Instance);
        var view = await q.GetPortfolioAsync("shopper1", CancellationToken.None);

        Assert.IsNotNull(view);
        Assert.AreEqual("balanced", view!.ModelId);
        Assert.AreEqual(0, view.Holdings.Count);
        Assert.AreEqual(0m, view.TotalValue);
    }
}
