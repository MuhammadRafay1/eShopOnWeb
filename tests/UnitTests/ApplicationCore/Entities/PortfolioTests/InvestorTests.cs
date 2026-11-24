using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PortfolioTests;

public class InvestorTests
{
    [Fact]
    public void NewInvestorHasNoUpvestIdentityOrModel()
    {
        var investor = new Investor("shopper@example.com");

        Assert.Equal("shopper@example.com", investor.ShopperUserName);
        Assert.Null(investor.UpvestUserId);
        Assert.Null(investor.UpvestAccountId);
        Assert.Null(investor.SelectedModelId);
    }

    [Fact]
    public void SetUpvestUserThenAccountPopulatesIdentity()
    {
        var investor = new Investor("shopper@example.com");

        investor.SetUpvestUser("user-1");
        Assert.Equal("user-1", investor.UpvestUserId);
        Assert.Null(investor.UpvestAccountId);

        investor.SetUpvestAccount("group-1", "account-1");
        Assert.Equal("group-1", investor.UpvestAccountGroupId);
        Assert.Equal("account-1", investor.UpvestAccountId);
    }

    [Fact]
    public void SelectModelCanBeSwitched()
    {
        var investor = new Investor("shopper@example.com");

        investor.SelectModel("balanced");
        Assert.Equal("balanced", investor.SelectedModelId);

        investor.SelectModel("growth");
        Assert.Equal("growth", investor.SelectedModelId);
    }

    [Fact]
    public void ConstructorRejectsEmptyUserName()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Investor(""));
    }
}
