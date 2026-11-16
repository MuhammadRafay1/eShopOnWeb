using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 3 — the signed-in investor pays an amount in; it is credited and invested at Upvest.</summary>
public sealed class CreatePaymentEndpoint : PortfolioEndpointBase, IEndpoint<IResult, CreatePaymentRequest>
{
    public CreatePaymentEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<CreatePaymentEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (CreatePaymentRequest request) => await HandleAsync(request))
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CreatePaymentRequest request)
    {
        var buyerId = BuyerId;
        if (buyerId is null)
        {
            return Results.Unauthorized();
        }

        if (request is null || !PortfolioShared.TryParseAmount(request.Amount, out var amount))
        {
            return Results.BadRequest(new { error = "amount must be a positive number with at most two decimal places." });
        }

        var investor = Store.GetInvestor(buyerId);
        if (investor is null || investor.UpvestUserId is not { } userId
            || investor.AccountId is not { } accountId || investor.AccountGroupId is not { } accountGroupId)
        {
            return Results.Conflict(new { error = "You must be an accepted investor with a ready account before paying in." });
        }

        if (investor.SelectedModelId is null)
        {
            return Results.Conflict(new { error = "Choose a model portfolio before paying in." });
        }

        InvestorStatus status;
        try
        {
            status = await PortfolioShared.ResolveInvestorStatusAsync(Gateway, userId, accountId, Ct);
        }
        catch (UpvestGatewayException ex)
        {
            return PortfolioShared.ToProblem(ex);
        }

        if (status != InvestorStatus.Active)
        {
            return Results.Conflict(new { error = "Upvest has not yet accepted you as an investor." });
        }

        // Idempotency: a caller-supplied Idempotency-Key header makes a retried pay-in safe; otherwise each
        // pay-in is a distinct payment.
        var idempotencyKey = ResolveIdempotencyKey(buyerId);
        var payment = new InvestorPayment(buyerId, amount, idempotencyKey);
        if (!Store.TryClaimPayment(payment, out var existing))
        {
            // A pay-in with this key already exists for this shopper — return it rather than paying twice.
            return await DescribeAsync(buyerId, existing);
        }

        try
        {
            var result = await Gateway.PayInAsync(
                userId, accountId, accountGroupId,
                investor.VirtualBankAccountId, investor.VirtualBankAccountIdempotencyKey,
                amount, payment.IdempotencyKey, payment.Id.ToString("N"), Ct);

            payment.PortfolioOrderId = result.PortfolioOrderId;
            Store.UpdatePayment(payment);

            if (investor.VirtualBankAccountId != result.VirtualBankAccountId)
            {
                investor.VirtualBankAccountId = result.VirtualBankAccountId;
                Store.UpdateInvestor(investor);
            }

            return Results.Created("api/portfolio/payments", new PaymentResponse
            {
                PaymentId = payment.Id.ToString(),
                Amount = amount,
                Status = PortfolioShared.ToWire(result.Status),
            });
        }
        catch (UpvestGatewayException ex)
        {
            // Definitive failure ⇒ release the claim so the shopper can pay in again. Unknown outcome ⇒ keep
            // the payment (the money may have been taken); it stays pending for reconciliation by its key.
            if (!ex.IsOutcomeUnknown)
            {
                Store.RemovePayment(buyerId, payment.Id);
            }

            Logger.LogWarning("Pay-in failed for a shopper (status {Status}).", ex.StatusCode);
            return PortfolioShared.ToProblem(ex);
        }
    }

    private async Task<IResult> DescribeAsync(string buyerId, InvestorPayment payment)
    {
        var status = PaymentStatus.Pending;
        if (payment.PortfolioOrderId is { } orderId)
        {
            try { status = await Gateway.GetPaymentStatusAsync(orderId, Ct); }
            catch (UpvestGatewayException) { /* fall back to pending */ }
        }

        return Results.Ok(new PaymentResponse
        {
            PaymentId = payment.Id.ToString(),
            Amount = payment.Amount,
            Status = PortfolioShared.ToWire(status),
        });
    }

    private Guid ResolveIdempotencyKey(string buyerId)
    {
        if (Http.Request.Headers.TryGetValue("Idempotency-Key", out var header))
        {
            var raw = header.ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (Guid.TryParse(raw, out var parsed))
                {
                    return parsed;
                }

                // Derive a stable GUID from (buyer, header) so an arbitrary token still deduplicates safely.
                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{buyerId}|{raw}"));
                return new Guid(bytes.Take(16).ToArray());
            }
        }

        return Guid.NewGuid();
    }
}

/// <summary>Flow 3 — the caller's payments, newest first, each with amount and status.</summary>
public sealed class ListPaymentsEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public ListPaymentsEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<ListPaymentsEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async () => await HandleAsync())
            .Produces<PaymentListResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        var buyerId = BuyerId;
        if (buyerId is null)
        {
            return Results.Unauthorized();
        }

        var response = new PaymentListResponse();
        foreach (var payment in Store.ListPayments(buyerId)) // already newest-first
        {
            var status = PaymentStatus.Pending;
            if (payment.PortfolioOrderId is { } orderId)
            {
                try { status = await Gateway.GetPaymentStatusAsync(orderId, Ct); }
                catch (UpvestGatewayException) { /* report pending if the provider read is unavailable */ }
            }

            response.Payments.Add(new PaymentResponse
            {
                PaymentId = payment.Id.ToString(),
                Amount = payment.Amount,
                Status = PortfolioShared.ToWire(status),
            });
        }

        return Results.Ok(response);
    }
}
