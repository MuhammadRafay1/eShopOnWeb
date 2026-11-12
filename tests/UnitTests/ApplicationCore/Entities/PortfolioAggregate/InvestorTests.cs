using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PortfolioAggregate;

public class InvestorTests
{
    [Fact]
    public void New_investor_is_pending_with_no_upvest_references()
    {
        var investor = new Investor("buyer-1", Guid.NewGuid(), "Karl Schmidt");

        Assert.Equal("buyer-1", investor.BuyerId);
        Assert.Equal(InvestorLifecycleStatus.Pending, investor.LifecycleStatus);
        Assert.Null(investor.UpvestUserId);
        Assert.False(investor.AccountReady);
        Assert.Null(investor.FollowedModelId);
    }

    [Fact]
    public void AccountReady_is_true_only_once_an_account_is_recorded()
    {
        var investor = new Investor("buyer-1", Guid.NewGuid(), "Karl Schmidt");
        Assert.False(investor.AccountReady);

        investor.RecordUser(Guid.NewGuid());
        investor.RecordAccountGroup(Guid.NewGuid());
        Assert.False(investor.AccountReady);

        investor.RecordAccount(Guid.NewGuid());
        Assert.True(investor.AccountReady);
    }

    [Fact]
    public void FollowModel_records_choice_and_configuration_flag()
    {
        var investor = new Investor("buyer-1", Guid.NewGuid(), "Karl Schmidt");

        investor.FollowModel("balanced", configurationExists: false);
        Assert.Equal("balanced", investor.FollowedModelId);
        Assert.False(investor.PortfolioConfigurationExists);

        investor.FollowModel("aggressive", configurationExists: true);
        Assert.Equal("aggressive", investor.FollowedModelId);
        Assert.True(investor.PortfolioConfigurationExists);
    }

    [Fact]
    public void The_signup_idempotency_key_is_stable_for_reconciling_retries()
    {
        var key = Guid.NewGuid();
        var investor = new Investor("buyer-1", key, "Karl Schmidt");

        Assert.Equal(key, investor.SignupIdempotencyKey);
    }
}
