using System.Net;
using Microsoft.eShopWeb.PublicApi.Portfolio;
using Xunit;

namespace Microsoft.eShopWeb.PortfolioUpvestTests;

public class GatewayTests
{
    private static readonly Guid Idem = Guid.NewGuid();

    [Fact]
    public async Task CreateUser_posts_to_users_with_sanitized_phone_and_returns_id()
    {
        var userId = Guid.NewGuid();
        var stub = new StubHttp().Enqueue(HttpStatusCode.Created,
            $"{{\"id\":\"{userId}\",\"created_at\":\"2024-01-01T00:00:00Z\",\"updated_at\":\"2024-01-01T00:00:00Z\",\"first_name\":\"Ada\",\"last_name\":\"Lovelace\",\"birth_date\":\"1985-12-10\",\"nationalities\":[\"DE\"],\"address\":{{\"address_line1\":\"Street 1\",\"postcode\":\"10117\",\"country\":\"DE\",\"city\":\"Berlin\"}},\"status\":\"ACTIVE\"}}");
        var gateway = TestGateway.Create(stub);

        var reg = new InvestorRegistration(
            "Ada", "Lovelace", "a@b.c", new DateTimeOffset(1985, 12, 10, 0, 0, 0, TimeSpan.Zero),
            "DE", "DE", "DE",
            new AddressInput("Street 1", "10117", "Berlin", "DE"),
            "+49 (30) 1234-5678", "DE123", "DE",
            new IdentityDocumentInput("PASSPORT", "X1", "DE", new DateTimeOffset(2032, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        var result = await gateway.CreateUserAsync(reg, Idem, default);

        Assert.Equal(userId, result);
        Assert.Equal(HttpMethod.Post, stub.LastApiRequest!.Method);
        Assert.EndsWith("/users", stub.LastApiRequest!.RequestUri!.AbsolutePath);
        // Phone stripped to digits only (Upvest pattern ^([0-9]{8,15})?$).
        Assert.Contains("\"phone_number\":\"493012345678\"", stub.LastApiBody);
        Assert.Contains("\"nationalities\":[\"DE\"]", stub.LastApiBody);
    }

    [Fact]
    public async Task CreateAllocation_posts_weights_as_strings_and_returns_allocation_id()
    {
        var allocationId = Guid.NewGuid();
        var stub = new StubHttp().Enqueue(HttpStatusCode.Created,
            $"{{\"id\":\"{allocationId}\",\"created_at\":\"2024-01-01T00:00:00Z\",\"updated_at\":\"2024-01-01T00:00:00Z\",\"name\":\"Balanced\",\"allocation\":[{{\"instrument_id\":\"IE00B4L5Y983\",\"weight\":\"60\"}}]}}");
        var gateway = TestGateway.Create(stub);

        var funds = new[] { new UpvestFund("IE00B4L5Y983", 60m), new UpvestFund("IE00B3RBWM25", 40m) };
        var id = await gateway.CreateAllocationAsync("Balanced", funds, Idem, default);

        Assert.Equal(allocationId, id);
        Assert.EndsWith("/portfolios/allocations", stub.LastApiRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("\"instrument_id\":\"IE00B4L5Y983\"", stub.LastApiBody);
        Assert.Contains("\"weight\":\"60\"", stub.LastApiBody);
        Assert.Contains("\"weight\":\"40\"", stub.LastApiBody);
    }

    [Fact]
    public async Task CreateTopup_sends_two_decimal_amount_and_eur()
    {
        var topupId = Guid.NewGuid();
        var stub = new StubHttp().Enqueue(HttpStatusCode.Created,
            $"{{\"id\":\"{topupId}\",\"created_at\":\"2024-01-01T00:00:00Z\",\"account_group_id\":\"{Guid.NewGuid()}\",\"cash_amount\":\"100.00\",\"currency\":\"EUR\",\"settlement_reference\":\"REF123\"}}");
        var gateway = TestGateway.Create(stub);

        var result = await gateway.CreateTopupAsync(Guid.NewGuid(), 100m, Idem, default);

        Assert.Equal(topupId, result.TopupId);
        Assert.Equal("REF123", result.SettlementReference);
        Assert.Contains("\"cash_amount\":\"100.00\"", stub.LastApiBody);
        Assert.Contains("\"currency\":\"EUR\"", stub.LastApiBody);
    }

    [Fact]
    public async Task GetPaymentState_maps_filled_order_to_invested()
    {
        var orderId = Guid.NewGuid();
        var stub = new StubHttp().Enqueue(HttpStatusCode.OK,
            $"{{\"id\":\"{orderId}\",\"created_at\":\"2024-01-01T00:00:00Z\",\"updated_at\":\"2024-01-01T00:00:00Z\",\"account_id\":\"{Guid.NewGuid()}\",\"cash_amount\":\"100.00\",\"currency\":\"EUR\",\"status\":\"FILLED\",\"orders\":[],\"client_reference\":\"ref\",\"initiation_flow\":\"API\"}}");
        var gateway = TestGateway.Create(stub);

        var state = await gateway.GetPaymentStateAsync(null, orderId, default);

        Assert.Equal(PaymentState.Invested, state);
    }

    [Fact]
    public async Task GetPaymentState_maps_cancelled_order_to_failed()
    {
        var orderId = Guid.NewGuid();
        var stub = new StubHttp().Enqueue(HttpStatusCode.OK,
            $"{{\"id\":\"{orderId}\",\"created_at\":\"2024-01-01T00:00:00Z\",\"updated_at\":\"2024-01-01T00:00:00Z\",\"account_id\":\"{Guid.NewGuid()}\",\"cash_amount\":\"100.00\",\"currency\":\"EUR\",\"status\":\"CANCELLED\",\"orders\":[],\"client_reference\":\"ref\",\"initiation_flow\":\"API\"}}");
        var gateway = TestGateway.Create(stub);

        var state = await gateway.GetPaymentStateAsync(null, orderId, default);

        Assert.Equal(PaymentState.Failed, state);
    }

    [Fact]
    public async Task Provider_4xx_on_write_surfaces_as_gateway_exception_with_status()
    {
        var stub = new StubHttp().Enqueue(HttpStatusCode.BadRequest,
            "{\"detail\":\"bad\",\"status\":400,\"title\":\"Invalid request\"}");
        var gateway = TestGateway.Create(stub);

        var ex = await Assert.ThrowsAsync<UpvestGatewayException>(
            () => gateway.CreateTopupAsync(Guid.NewGuid(), 10m, Idem, default));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.False(ex.OutcomeUnknown);
    }

    [Fact]
    public async Task Connection_failure_on_write_is_marked_outcome_unknown()
    {
        var stub = new StubHttp().Throw(new HttpRequestException("connection reset"));
        var gateway = TestGateway.Create(stub);

        var ex = await Assert.ThrowsAsync<UpvestGatewayException>(
            () => gateway.PlaceBuyOrderAsync(Guid.NewGuid(), Guid.NewGuid(), 50m, Idem, default));

        // A transport failure on a write leaves the outcome unknown (settle on the next read),
        // never a definite failure.
        Assert.True(ex.OutcomeUnknown);
    }

    [Fact]
    public async Task Unsupported_nationality_is_rejected_before_any_call()
    {
        var stub = new StubHttp(); // no responders enqueued: no API call should happen
        var gateway = TestGateway.Create(stub);

        var reg = new InvestorRegistration(
            "Ada", "L", "a@b.c", DateTimeOffset.UnixEpoch, null, "ZZ", "ZZ",
            new AddressInput("S", "1", "C", "ZZ"), null, "T", "ZZ",
            new IdentityDocumentInput("PASSPORT", "X", "ZZ", DateTimeOffset.UnixEpoch));

        var ex = await Assert.ThrowsAsync<UpvestGatewayException>(
            () => gateway.CreateUserAsync(reg, Idem, default));
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Empty(stub.Requests);
    }
}
