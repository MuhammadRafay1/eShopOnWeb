using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>
/// GET /api/reconciliation?from={from}&amp;to={to} — list PayPal's transactions for a date range and line them
/// up against eShop orders, in both directions. (operator/admin)
/// </summary>
public class GetReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IReconciliationService service, CancellationToken ct) =>
                await HandleAsync(new ReconciliationRequest(from, to), service, ct))
            .Produces<ReconciliationReport>()
            .WithTags("ReconciliationEndpoints");
    }

    public Task<IResult> HandleAsync(ReconciliationRequest request, IReconciliationService service) =>
        HandleAsync(request, service, default);

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IReconciliationService service, CancellationToken ct)
    {
        if (request.To < request.From)
        {
            throw new BadPaymentRequestException("'to' must be on or after 'from'.");
        }

        var report = await service.ReconcileAsync(request.From, request.To, ct);
        return Results.Ok(report);
    }
}

public record ReconciliationRequest(DateTimeOffset From, DateTimeOffset To);
