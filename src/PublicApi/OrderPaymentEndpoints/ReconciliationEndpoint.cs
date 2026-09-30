using System;
using System.Linq;
using System.Threading.Tasks;
using BlazorShared.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class ReconciliationQuery
{
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
}

public class ReconciliationResponse
{
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
    public MatchedReconciliationEntry[] Matched { get; init; } = Array.Empty<MatchedReconciliationEntry>();
    public PayPalOnlyTransaction[] PayPalOnly { get; init; } = Array.Empty<PayPalOnlyTransaction>();
    public EShopOnlyPayment[] EShopOnly { get; init; } = Array.Empty<EShopOnlyPayment>();
}

/// <summary>
/// Operator action: lines up PayPal's own transaction record for a date range against local
/// eShop payments. Covers the whole range (chunked/paginated internally), not just its first
/// page. An empty result for a very recent range is expected - PayPal's transaction reporting
/// lags live activity by up to ~3 hours.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationQuery, IReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (DateTimeOffset from, DateTimeOffset to, IReconciliationService reconciliationService) =>
            {
                return await HandleAsync(new ReconciliationQuery { From = from, To = to }, reconciliationService);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(ReconciliationQuery request, IReconciliationService reconciliationService)
    {
        if (request.To <= request.From)
        {
            return Results.BadRequest(new { message = "'to' must be after 'from'." });
        }

        var report = await reconciliationService.ReconcileAsync(request.From, request.To);
        var response = new ReconciliationResponse
        {
            From = report.From,
            To = report.To,
            Matched = report.Matched.ToArray(),
            PayPalOnly = report.PayPalOnly.ToArray(),
            EShopOnly = report.EShopOnly.ToArray()
        };
        return Results.Ok(response);
    }
}
