using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.Investing.Upvest;

/// <summary>A newly created Upvest user.</summary>
public record UpvestUser(string Id, string Status);

/// <summary>A snapshot of an Upvest order and its executions.</summary>
public record UpvestOrderSnapshot(string Id, string Status, IReadOnlyList<string> ExecutionStatuses);
