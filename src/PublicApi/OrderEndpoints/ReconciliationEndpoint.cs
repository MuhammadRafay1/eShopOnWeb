using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// GET api/reconciliation?from=&amp;to= — PayPal's own transaction records for an ISO-8601 date-time
/// range, lined up against eShop orders so either side knowing about a payment the other does not is
/// visible. Covers the whole range (paged internally). Administrator role only.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (DateTimeOffset from, DateTimeOffset to, IPaymentService service, CancellationToken ct) =>
                await Run(new ReconciliationRequest(from, to), service, ct))
            .Produces<ReconciliationView>()
            .WithTags("OrderEndpoints");
    }

    public Task<IResult> HandleAsync(ReconciliationRequest request, IPaymentService service) =>
        Run(request, service, CancellationToken.None);

    private static async Task<IResult> Run(ReconciliationRequest request, IPaymentService service, CancellationToken ct)
    {
        if (request.To < request.From)
            return Results.BadRequest(new { message = "'to' must not be earlier than 'from'." });
        return Results.Ok(await service.ReconcileAsync(request.From, request.To, ct));
    }
}

public sealed record ReconciliationRequest(DateTimeOffset From, DateTimeOffset To);
