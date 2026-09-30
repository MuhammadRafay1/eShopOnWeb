using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class ReconciliationRequest
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public ReconciliationRequest(DateTimeOffset from, DateTimeOffset to)
    {
        From = from;
        To = to;
    }
    public ReconciliationRequest() { }
}

/// <summary>
/// Operator report: PayPal's own transaction record for a date range lined up against eShop
/// payments, surfacing PayPal-only and eShop-only discrepancies. Covers the whole range (chunked
/// and paged internally). Restricted to administrators.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IPaymentService paymentService, CancellationToken ct) =>
            {
                return await HandleAsync(new ReconciliationRequest(from, to), paymentService, ct);
            })
            .Produces<ReconciliationReport>()
            .WithTags("Reconciliation");
    }

    public Task<IResult> HandleAsync(ReconciliationRequest request, IPaymentService paymentService)
        => HandleAsync(request, paymentService, CancellationToken.None);

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IPaymentService paymentService, CancellationToken ct)
    {
        var report = await paymentService.ReconcileAsync(request.From, request.To, ct);
        return Results.Ok(report);
    }
}
