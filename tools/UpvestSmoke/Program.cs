using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.eShopWeb.Infrastructure.Upvest.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var settings = new Dictionary<string, string?>
{
    ["Upvest:ClientId"] = Environment.GetEnvironmentVariable("UPVEST_CLIENT_ID"),
    ["Upvest:ClientSecret"] = Environment.GetEnvironmentVariable("UPVEST_CLIENT_SECRET"),
    ["Upvest:SigningKeyId"] = Environment.GetEnvironmentVariable("UPVEST_SIGNING_KEY_ID"),
    ["Upvest:SigningKeyPath"] = Environment.GetEnvironmentVariable("UPVEST_SIGNING_KEY_PATH"),
    ["Upvest:SigningKeyPassphrase"] = Environment.GetEnvironmentVariable("UPVEST_SIGNING_KEY_PASSPHRASE"),
    ["Upvest:BaseUrl"] = Environment.GetEnvironmentVariable("UPVEST_BASE_URL"),
};

var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
var services = new ServiceCollection();
services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
services.AddUpvestIntegration(config);
var provider = services.BuildServiceProvider();

var tokens = provider.GetRequiredService<UpvestTokenProvider>();
var ct = CancellationToken.None;

string[] scopes =
{
    "users:admin", "users:read", "users:admin users:read", "checks:admin", "checks:read",
    "accounts:admin", "accounts:read", "portfolios:admin", "portfolios:read",
    "payments:admin", "valuations:read", "positions:read"
};

Console.WriteLine("== Probing which scopes this sandbox client can obtain a token for ==");
foreach (var scope in scopes)
{
    try
    {
        var token = await tokens.GetTokenAsync(scope, ct, forceRefresh: true);
        Console.WriteLine($"   OK     [{scope}]  token len={token.Length}");
    }
    catch (UpvestApiException ex)
    {
        Console.WriteLine($"   DENIED [{scope}]  {ex.Message}");
    }
}

// If users:admin works, exercise create+get user end to end.
try
{
    var client = provider.GetRequiredService<IUpvestClient>();
    Console.WriteLine("\n== Create + Get user (end to end) ==");
    var createReq = new CreateUserRequest(
        FirstName: "Ada", LastName: "Lovelace",
        Email: $"ada.{Guid.NewGuid():N}@example.com",
        BirthDate: "1985-12-10", Nationalities: new[] { "GB" },
        Address: new UpvestAddress("221B Baker Street", "NW16XE", "London", "GB"),
        Fatca: new UpvestFatca(false, DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
        BirthCity: "London", BirthCountry: "GB", PhoneNumber: "441234567890");
    var created = await client.CreateUserAsync(createReq, ct);
    Console.WriteLine($"   create: success={created.IsSuccess} status={created.StatusCode} id={created.Value?.Id} userStatus={created.Value?.Status} err={created.ErrorSummary}");
    if (created.IsSuccess && created.Value is not null)
    {
        var got = await client.GetUserAsync(created.Value.Id, ct);
        Console.WriteLine($"   get:    success={got.IsSuccess} status={got.StatusCode} userStatus={got.Value?.Status} err={got.ErrorSummary}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"   user flow error: {ex.Message}");
}

Console.WriteLine("Done.");
