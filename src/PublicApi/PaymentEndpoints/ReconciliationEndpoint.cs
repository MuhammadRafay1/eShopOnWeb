using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class ReconciliationResponse
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public List<ReconciliationMatchDto> Matched { get; set; } = new();
    public List<PayPalOnlyDto> PayPalOnly { get; set; } = new();
    public List<LocalOnlyDto> LocalOnly { get; set; } = new();
    public ReconciliationSummaryDto Summary { get; set; } = new();
}

public class ReconciliationMatchDto
{
    public string PayPalTransactionId { get; set; } = "";
    public decimal? PayPalAmount { get; set; }
    public string? PayPalStatus { get; set; }
    public int OrderId { get; set; }
    public string LocalReference { get; set; } = "";
    public string LocalReferenceKind { get; set; } = "";
}

public class PayPalOnlyDto
{
    public string PayPalTransactionId { get; set; } = "";
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? InitiatedAt { get; set; }
    public string? InvoiceId { get; set; }
}

public class LocalOnlyDto
{
    public int OrderId { get; set; }
    public string PayPalReference { get; set; } = "";
    public string ReferenceKind { get; set; } = "";
    public decimal? Amount { get; set; }
}

public class ReconciliationSummaryDto
{
    public int PayPalTransactionCount { get; set; }
    public int LocalReferenceCount { get; set; }
    public int MatchedCount { get; set; }
    public int PayPalOnlyCount { get; set; }
    public int LocalOnlyCount { get; set; }
}

/// <summary>Operator report: PayPal's transactions for a date range, lined up against local eShop payments.</summary>
public class ReconciliationEndpoint : IEndpoint<IResult, IReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IReconciliationService service, CancellationToken ct) =>
            {
                return await HandleAsync(from, to, service, ct);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("OrderPaymentEndpoints");
    }

    public Task<IResult> HandleAsync(IReconciliationService service) =>
        throw new NotSupportedException("Use the route handler, which supplies the date range.");

    private async Task<IResult> HandleAsync(DateTimeOffset from, DateTimeOffset to, IReconciliationService service, CancellationToken ct)
    {
        var report = await service.BuildReportAsync(from, to, ct);

        var response = new ReconciliationResponse
        {
            From = report.From,
            To = report.To,
            Matched = report.Matched.Select(m => new ReconciliationMatchDto
            {
                PayPalTransactionId = m.PayPalTransactionId,
                PayPalAmount = m.PayPalAmount,
                PayPalStatus = m.PayPalStatus,
                OrderId = m.OrderId,
                LocalReference = m.LocalPayPalReference,
                LocalReferenceKind = m.LocalReferenceKind
            }).ToList(),
            PayPalOnly = report.PayPalOnly.Select(p => new PayPalOnlyDto
            {
                PayPalTransactionId = p.PayPalTransactionId,
                Amount = p.Amount,
                CurrencyCode = p.CurrencyCode,
                Status = p.Status,
                InitiatedAt = p.InitiatedAt,
                InvoiceId = p.InvoiceId
            }).ToList(),
            LocalOnly = report.LocalOnly.Select(l => new LocalOnlyDto
            {
                OrderId = l.OrderId,
                PayPalReference = l.PayPalReference,
                ReferenceKind = l.ReferenceKind,
                Amount = l.Amount
            }).ToList(),
            Summary = new ReconciliationSummaryDto
            {
                PayPalTransactionCount = report.Summary.PayPalTransactionCount,
                LocalReferenceCount = report.Summary.LocalReferenceCount,
                MatchedCount = report.Summary.MatchedCount,
                PayPalOnlyCount = report.Summary.PayPalOnlyCount,
                LocalOnlyCount = report.Summary.LocalOnlyCount
            }
        };
        return Results.Ok(response);
    }
}
