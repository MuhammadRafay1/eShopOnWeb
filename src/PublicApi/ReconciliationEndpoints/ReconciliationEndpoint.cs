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
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

public class ReconciliationRequest
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
}

public class MatchedTransactionDto
{
    public string PayPalTransactionId { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public decimal PayPalAmount { get; set; }
    public decimal EShopCapturedAmount { get; set; }
    public string PayPalStatus { get; set; } = string.Empty;
    public bool AmountsMatch { get; set; }
}

public class UnmatchedPayPalTransactionDto
{
    public string PayPalTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InvoiceId { get; set; }
    public string? CustomField { get; set; }
}

public class UnmatchedEShopPaymentDto
{
    public int OrderId { get; set; }
    public string? InvoiceId { get; set; }
    public decimal? CapturedAmount { get; set; }
    public string? CaptureStatus { get; set; }
}

public class ReconciliationResponse
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public List<MatchedTransactionDto> Matched { get; set; } = new();
    public List<UnmatchedPayPalTransactionDto> InPayPalNotInEShop { get; set; } = new();
    public List<UnmatchedEShopPaymentDto> InEShopNotInPayPal { get; set; } = new();
    public string Note { get; set; } =
        "PayPal transaction reporting can lag live activity by up to ~3 hours; an empty result for a range you just created is expected, not a gap.";
}

/// <summary>
/// Operator report lining PayPal's own transaction record up against eShop orders for a date
/// range, so a payment PayPal knows about that eShop doesn't (or vice versa) is visible. Pages
/// through the entire requested range, not just its first page.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, ReconciliationEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, ReconciliationEndpointServices services) =>
            {
                return await HandleAsync(new ReconciliationRequest { From = from, To = to }, services);
            })
            .Produces<ReconciliationResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("ReconciliationEndpoints");
    }

    public async Task<IResult> HandleAsync(ReconciliationRequest request, ReconciliationEndpointServices services)
    {
        if (request.From > request.To)
            return Results.BadRequest("from must be earlier than or equal to to.");

        var transactions = await services.PaymentGateway.SearchTransactionsAsync(request.From, request.To);
        var eshopPayments = await services.PaymentRepository.ListAsync(new PaymentsCreatedInRangeSpecification(request.From, request.To));

        var eshopByOrderId = eshopPayments.ToDictionary(p => p.OrderId.ToString(), p => p);
        var matchedOrderIds = new HashSet<int>();

        var response = new ReconciliationResponse { From = request.From, To = request.To };

        foreach (var txn in transactions)
        {
            Payment? match = null;
            if (!string.IsNullOrEmpty(txn.CustomField) && eshopByOrderId.TryGetValue(txn.CustomField, out var byCustomField))
                match = byCustomField;
            else if (!string.IsNullOrEmpty(txn.InvoiceId))
                match = eshopPayments.FirstOrDefault(p => p.InvoiceId == txn.InvoiceId);

            if (match is not null)
            {
                matchedOrderIds.Add(match.OrderId);
                response.Matched.Add(new MatchedTransactionDto
                {
                    PayPalTransactionId = txn.TransactionId,
                    OrderId = match.OrderId,
                    PayPalAmount = txn.Amount,
                    EShopCapturedAmount = match.CapturedAmount ?? 0m,
                    PayPalStatus = txn.Status,
                    AmountsMatch = match.CapturedAmount.HasValue && Math.Abs(match.CapturedAmount.Value - txn.Amount) < 0.01m
                });
            }
            else
            {
                response.InPayPalNotInEShop.Add(new UnmatchedPayPalTransactionDto
                {
                    PayPalTransactionId = txn.TransactionId,
                    Amount = txn.Amount,
                    Status = txn.Status,
                    InvoiceId = txn.InvoiceId,
                    CustomField = txn.CustomField
                });
            }
        }

        response.InEShopNotInPayPal = eshopPayments
            .Where(p => !matchedOrderIds.Contains(p.OrderId))
            .Select(p => new UnmatchedEShopPaymentDto
            {
                OrderId = p.OrderId,
                InvoiceId = p.InvoiceId,
                CapturedAmount = p.CapturedAmount,
                CaptureStatus = p.CaptureStatus
            }).ToList();

        return Results.Ok(response);
    }
}

public class ReconciliationEndpointServices
{
    public ReconciliationEndpointServices(IRepository<Payment> paymentRepository, IPayPalPaymentGateway paymentGateway)
    {
        PaymentRepository = paymentRepository;
        PaymentGateway = paymentGateway;
    }

    public IRepository<Payment> PaymentRepository { get; }
    public IPayPalPaymentGateway PaymentGateway { get; }
}
