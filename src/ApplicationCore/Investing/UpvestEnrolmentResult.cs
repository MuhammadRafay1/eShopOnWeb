using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The Upvest references created when a shopper's sign-up form is submitted (the user and their KYC
/// check). Returned by <see cref="Interfaces.IUpvestInvestorGateway.EnrolInvestorAsync"/>. The trading
/// account is provisioned later, once Upvest has activated the user.
/// </summary>
public class UpvestEnrolmentResult
{
    public UpvestEnrolmentResult(Guid userId, Guid kycCheckId)
    {
        UserId = userId;
        KycCheckId = kycCheckId;
    }

    public Guid UserId { get; }
    public Guid KycCheckId { get; }
}

/// <summary>
/// The Upvest account group and trading account provisioned for an accepted investor. Returned by
/// <see cref="Interfaces.IUpvestInvestorGateway.ProvisionAccountsAsync"/>.
/// </summary>
public class UpvestAccountRefs
{
    public UpvestAccountRefs(Guid accountGroupId, Guid accountId)
    {
        AccountGroupId = accountGroupId;
        AccountId = accountId;
    }

    public Guid AccountGroupId { get; }
    public Guid AccountId { get; }
}
