using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class ReconciliationRequest : BaseRequest
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
}

public class ReconciliationResponse : BaseResponse
{
    public List<ReconciliationEntryDto> Entries { get; set; } = new();
    public int PagesFetched { get; set; }
    public int TotalPages { get; set; }
    public bool Truncated { get; set; }
}

/// <summary>
/// Operator report: PayPal's own transactions for a date range, lined up against eShop orders.
/// PayPal's reporting lags live activity - a range covering just-created payments may legitimately come back empty.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IReconciliationService reconciliationService) =>
            {
                return await HandleAsync(new ReconciliationRequest { From = from, To = to }, reconciliationService);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IReconciliationService reconciliationService)
    {
        var report = await reconciliationService.GetReportAsync(request.From, request.To, default);

        var response = new ReconciliationResponse
        {
            Entries = report.Entries.Select(e => new ReconciliationEntryDto
            {
                PayPalTransactionId = e.PayPalTransactionId,
                PayPalStatus = e.PayPalStatus,
                PayPalAmount = e.PayPalAmount,
                CurrencyCode = e.CurrencyCode,
                OrderId = e.OrderId,
                EshopPaymentStatus = e.EshopPaymentStatus,
                MatchState = e.MatchState
            }).ToList(),
            PagesFetched = report.PagesFetched,
            TotalPages = report.TotalPages,
            Truncated = report.Truncated
        };
        return Results.Ok(response);
    }
}
