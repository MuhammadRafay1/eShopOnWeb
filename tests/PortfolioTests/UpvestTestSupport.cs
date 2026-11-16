using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.eShopWeb.Infrastructure.Portfolio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Hooks;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.PortfolioTests;

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that answers the OAuth token request automatically and serves every
/// other (operation) request from a FIFO queue, in call order. This makes tests route-agnostic — the exact
/// Upvest paths are irrelevant; only the operation sequence matters.
/// </summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Json)> _responses;
    private readonly bool _failAllWith401;

    public List<string> RequestedPaths { get; } = new();
    public List<string?> Bodies { get; } = new();

    /// <summary>Number of leading non-token requests to fail with a dropped connection before serving the queue.</summary>
    public int ThrowFirst { get; set; }

    private int _nonTokenSeen;

    public StubHandler(IEnumerable<(HttpStatusCode, string)> responses)
    {
        _responses = new Queue<(HttpStatusCode, string)>(responses);
    }

    private StubHandler(bool failAllWith401)
    {
        _responses = new Queue<(HttpStatusCode, string)>();
        _failAllWith401 = failAllWith401;
    }

    /// <summary>A handler whose non-token requests always return the given status with an empty JSON body.</summary>
    public static StubHandler AlwaysStatus(HttpStatusCode status) => new(failAllWith401: true) { Override = status };

    public HttpStatusCode Override { get; private init; } = HttpStatusCode.Unauthorized;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        RequestedPaths.Add(path);
        Bodies.Add(request.Content is null ? null : request.Content.ReadAsStringAsync(cancellationToken).Result);

        // The SDK must obtain an access token before any operation; answer that automatically.
        if (path.Contains("/auth/token", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Json(HttpStatusCode.OK,
                """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}"""));
        }

        if (_failAllWith401)
        {
            return Task.FromResult(Json(Override, """{"error":"this client is not authorised to use these scopes"}"""));
        }

        _nonTokenSeen++;
        if (_nonTokenSeen <= ThrowFirst)
        {
            throw new HttpRequestException("connection reset"); // the SDK surfaces this as SdkConnectionException
        }

        var (status, body) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.InternalServerError, "{}");
        return Task.FromResult(Json(status, body));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public static class UpvestTestClient
{
    /// <summary>Builds a real <see cref="UpvestInvestmentApiClient"/> over the stub, with a throwaway EC signing key.</summary>
    public static (UpvestPortfolioGateway Gateway, StubHandler Handler) BuildGateway(params (HttpStatusCode, string)[] responses)
    {
        var handler = new StubHandler(responses);
        return (BuildGatewayWith(handler), handler);
    }

    public static UpvestPortfolioGateway BuildGatewayWith(StubHandler handler)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = ecdsa.ExportECPrivateKeyPem();
        var keyPath = Path.Combine(Path.GetTempPath(), $"upvest-test-{Guid.NewGuid():N}.pem");
        File.WriteAllText(keyPath, pem);

        var options = new UpvestInvestmentApiClientOptions
        {
            Environment = ServerEnvironment.Production,
            OauthClientCredentials = new OAuth2ClientCredentials { ClientId = "test-client", ClientSecret = "test-secret" },
            Hooks = new SdkHook[]
            {
                new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile("key-1", keyPath, "test-client", null)),
            },
        };
        options.Server.Default.Production.BaseUrl = "https://sandbox.upvest.co";

        var client = new UpvestInvestmentApiClient(new HttpClient(handler), options);
        var settings = Options.Create(new UpvestSettings
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            SigningKeyId = "key-1",
            SigningKeyPath = keyPath,
            BaseUrl = "https://sandbox.upvest.co",
            RequestTimeoutSeconds = 30,
        });
        return new UpvestPortfolioGateway(client, settings, NullLogger<UpvestPortfolioGateway>.Instance);
    }
}

/// <summary>Minimal valid JSON payloads for the Upvest response models the gateway reads.</summary>
public static class Payloads
{
    private const string Ts = "2024-01-01T00:00:00Z";

    public static string UserTol(Guid id, string status = "ACTIVE") =>
        $$"""
        {"id":"{{id}}","created_at":"{{Ts}}","updated_at":"{{Ts}}","first_name":"Erika","last_name":"Mustermann",
         "birth_date":"1985-04-12","nationalities":["DE"],
         "address":{"address_line1":"Alexanderplatz 1","postcode":"10178","country":"DE","city":"Berlin"},
         "status":"{{status}}"}
        """;

    public static string CheckCreated(Guid id) => $$"""{"id":"{{id}}"}""";

    public static string TaxRecord() =>
        $$"""{"created_at":"{{Ts}}","updated_at":"{{Ts}}","tax_residencies":[],"status":"ACTIVE"}""";

    public static string AccountGroup(Guid id, string status = "PENDING_APPROVAL") =>
        $$"""
        {"id":"{{id}}","created_at":"{{Ts}}","updated_at":"{{Ts}}","users":[],"status":"{{status}}",
         "type":"PERSONAL","securities_account_number":"1234567"}
        """;

    public static string Account(Guid id, Guid accountGroupId, string status = "ACTIVE") =>
        $$"""
        {"id":"{{id}}","created_at":"{{Ts}}","updated_at":"{{Ts}}","account_group_id":"{{accountGroupId}}",
         "type":"PORTFOLIO","users":[],"account_number":1,"name":"Portfolio","status":"{{status}}"}
        """;

    public static string Allocation(Guid id, string name = "Balanced") =>
        $$"""
        {"id":"{{id}}","created_at":"{{Ts}}","updated_at":"{{Ts}}","name":"{{name}}",
         "allocation":[{"instrument_id":"IE00B4L5Y983","weight":"60"},{"instrument_id":"IE00B3RBWM25","weight":"40"}]}
        """;

    public static string AllocationsList(Guid id) =>
        $$"""{"meta":{"offset":0,"limit":100,"count":1,"total_count":1},"data":[{{Allocation(id)}}]}""";

    public static string BankTransaction(Guid id) => $$"""{"id":"{{id}}","bank_reference":"BR-1"}""";

    public static string Order(Guid id, string status = "FILLED") =>
        $$"""
        {"id":"{{id}}","created_at":"{{Ts}}","updated_at":"{{Ts}}","account_id":"{{Guid.NewGuid()}}",
         "cash_amount":"100.00","currency":"EUR","status":"{{status}}","post_tax":false,"orders":[],
         "client_reference":"ref","initiation_flow":"API"}
        """;

    public static string RebalancingTrigger(Guid id) => $$"""{"id":"{{id}}"}""";

    public static string ExecutionOrders(params string[] statuses)
    {
        var items = string.Join(",", statuses.Select(s =>
            $$"""
            {"id":"{{Guid.NewGuid()}}","execution_id":"{{Guid.NewGuid()}}","account_id":"{{Guid.NewGuid()}}",
             "portfolio_order_id":null,"status":"{{s}}","created_at":"{{Ts}}","updated_at":"{{Ts}}"}
            """));
        return $$"""{"meta":{"offset":0,"limit":100,"count":{{statuses.Length}},"total_count":{{statuses.Length}}},"data":[{{items}}]}""";
    }

    public static string Valuation(Guid accountId) =>
        $$"""
        {"id":"{{Guid.NewGuid()}}","created_at":"{{Ts}}","updated_at":"{{Ts}}","account_id":"{{accountId}}",
         "total_security_value":{"amount":"1000.00","currency":"EUR"},"price_quality":"HIGHEST_AVAILABLE",
         "security_positions":[
           {"instrument":{"uuid":"{{Guid.NewGuid()}}","isin":"IE00B4L5Y983"},"quantity":"10.5",
            "value":{"amount":"600.00","currency":"EUR","price_time":"{{Ts}}"},"weight":"60"},
           {"instrument":{"uuid":"{{Guid.NewGuid()}}","isin":"IE00B3RBWM25"},"quantity":"5.0",
            "value":{"amount":"400.00","currency":"EUR","price_time":"{{Ts}}"},"weight":"40"}],
         "valuation_time":"{{Ts}}"}
        """;
}
