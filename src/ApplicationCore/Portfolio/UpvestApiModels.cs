using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>Result of creating/fetching an Upvest user.</summary>
public record UpvestUser(string Id, string Status);

/// <summary>Result of creating/fetching an Upvest account group.</summary>
public record UpvestAccountGroup(string Id, string Status);

/// <summary>Result of creating/fetching an Upvest account.</summary>
public record UpvestAccount(string Id, string Status);

/// <summary>Result of creating/fetching an Upvest portfolio order.</summary>
public record UpvestPortfolioOrder(string Id, string Status);

/// <summary>A single security position as recorded by Upvest.</summary>
public record UpvestPosition(string InstrumentId, decimal Quantity, decimal Value);

/// <summary>An account's current valuation at Upvest.</summary>
public record UpvestValuation(IReadOnlyList<UpvestPosition> Positions, decimal TotalValue);
