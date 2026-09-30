using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

public class ReconciliationRequest : BaseRequest
{
    public DateTimeOffset From { get; init; }
    public DateTimeOffset To { get; init; }
    public ReconciliationRequest(DateTimeOffset from, DateTimeOffset to)
    {
        From = from;
        To = to;
    }
}

public class ReconciliationRow
{
    public string MatchStatus { get; set; } = "";       // Matched | PayPalOnly | EShopOnly
    public string TransactionId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public string? PayPalStatus { get; set; }
    public int? EShopOrderId { get; set; }
    public string? EShopKind { get; set; }                // Capture | Refund
}

public class ReconciliationSummary
{
    public int MatchedCount { get; set; }
    public int PayPalOnlyCount { get; set; }
    public int EShopOnlyCount { get; set; }
}

public class ReconciliationResponse : BaseResponse
{
    public ReconciliationResponse(Guid correlationId) : base(correlationId) { }
    public ReconciliationResponse() { }

    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public List<ReconciliationRow> Rows { get; set; } = new();
    public ReconciliationSummary Summary { get; set; } = new();
}

/// <summary>
/// GET api/reconciliation?from=...&amp;to=... — line up PayPal's own record of transactions for a date
/// range against eShop's captures and refunds, so a discrepancy either way is visible. Operator only.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IRepository<Payment>>
{
    private readonly IPaymentGatewayService _gateway;

    public ReconciliationEndpoint(IPaymentGatewayService gateway) => _gateway = gateway;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IRepository<Payment> paymentRepository) =>
            {
                return await HandleAsync(new ReconciliationRequest(from, to), paymentRepository);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("ReconciliationEndpoints");
    }

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IRepository<Payment> paymentRepository)
    {
        if (request.From > request.To)
            return Results.BadRequest("'from' must not be after 'to'.");

        // PayPal's own record (paged over the whole range inside the gateway).
        var payPalTxns = await _gateway.SearchTransactionsAsync(request.From, request.To);
        var payPalById = payPalTxns
            .GroupBy(t => t.TransactionId)
            .ToDictionary(g => g.Key, g => g.First());

        // eShop's own record: every capture and every refund whose timestamp falls in range.
        var payments = await paymentRepository.ListAsync(new PaymentsWithRefundsSpecification());
        var localTxns = new List<(string Id, decimal Amount, string Currency, int OrderId, string Kind)>();

        foreach (var p in payments)
        {
            if (p.CaptureId is not null && p.CapturedAt is { } capturedAt &&
                capturedAt >= request.From && capturedAt <= request.To)
            {
                localTxns.Add((p.CaptureId, p.CapturedAmount ?? 0m, p.Currency, p.OrderId, "Capture"));
            }

            foreach (var r in p.Refunds)
            {
                if (r.CreatedAt >= request.From && r.CreatedAt <= request.To)
                    localTxns.Add((r.PayPalRefundId, r.Amount, p.Currency, p.OrderId, "Refund"));
            }
        }

        var localById = localTxns
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<ReconciliationRow>();

        // Every PayPal transaction: matched if eShop also has it, else PayPal-only.
        foreach (var t in payPalTxns)
        {
            localById.TryGetValue(t.TransactionId, out var local);
            bool matched = localById.ContainsKey(t.TransactionId);
            rows.Add(new ReconciliationRow
            {
                MatchStatus = matched ? "Matched" : "PayPalOnly",
                TransactionId = t.TransactionId,
                Amount = t.Amount,
                Currency = t.Currency,
                PayPalStatus = t.Status,
                EShopOrderId = matched ? local.OrderId : null,
                EShopKind = matched ? local.Kind : null
            });
        }

        // Local transactions PayPal did not (yet) report — expected for very recent activity.
        foreach (var local in localTxns.Where(l => !payPalById.ContainsKey(l.Id)))
        {
            rows.Add(new ReconciliationRow
            {
                MatchStatus = "EShopOnly",
                TransactionId = local.Id,
                Amount = local.Amount,
                Currency = local.Currency,
                PayPalStatus = null,
                EShopOrderId = local.OrderId,
                EShopKind = local.Kind
            });
        }

        var response = new ReconciliationResponse(request.CorrelationId())
        {
            From = request.From,
            To = request.To,
            Rows = rows,
            Summary = new ReconciliationSummary
            {
                MatchedCount = rows.Count(r => r.MatchStatus == "Matched"),
                PayPalOnlyCount = rows.Count(r => r.MatchStatus == "PayPalOnly"),
                EShopOnlyCount = rows.Count(r => r.MatchStatus == "EShopOnly")
            }
        };

        return Results.Ok(response);
    }
}
