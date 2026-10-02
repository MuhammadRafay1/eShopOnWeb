using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>
/// GET /api/reconciliation?from={from}&amp;to={to} — operator report lining PayPal's transactions up
/// against eShop orders over the whole range. `from`/`to` are ISO-8601 date-times.
/// </summary>
public class ReconciliationEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                string from,
                string to,
                IReconciliationService service,
                CancellationToken cancellationToken) =>
            {
                if (!TryParseIso(from, out var fromDate))
                {
                    throw new PaymentValidationException("'from' must be an ISO-8601 date-time.");
                }
                if (!TryParseIso(to, out var toDate))
                {
                    throw new PaymentValidationException("'to' must be an ISO-8601 date-time.");
                }

                var report = await service.ReconcileAsync(fromDate, toDate, cancellationToken);
                return Results.Ok(report);
            })
            .WithTags("PaymentEndpoints");
    }

    private static bool TryParseIso(string value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
}
