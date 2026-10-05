namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>Internal enrolment progression stage, driven by the background reconciler.</summary>
public enum EnrolmentStage
{
    /// <summary>User, KYC check and tax residency submitted; waiting for Upvest to activate the user.</summary>
    AwaitingUserActivation,

    /// <summary>Account group + trading account created; waiting for the trading account to become active.</summary>
    AwaitingAccountActivation,

    /// <summary>Fully onboarded and accepted — the shopper can invest.</summary>
    Active,

    /// <summary>Upvest rejected the shopper.</summary>
    Rejected
}
