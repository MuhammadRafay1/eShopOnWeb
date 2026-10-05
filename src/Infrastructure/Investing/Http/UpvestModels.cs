using System.Collections.Generic;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Http;

/// <summary>Minimal projections of Upvest responses — only the fields this integration uses.</summary>
public record UpvestResource(string Id, string Status);

public record UpvestWebhook(string Id, string Url, bool Enabled);

public record UpvestVerifyKey(string Kid, string Crv, string X, string Y);

public record UpvestOrder(string Id, string Status, IReadOnlyList<string> ExecutionStatuses);
