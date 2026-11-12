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
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// POST /api/portfolio/payments — the signed-in investor pays an amount in (euros).
/// </summary>
public class PayInEndpoint : IEndpoint<IResult, PayInRequest, HttpContext>
{
    private readonly IUpvestInvestmentService _service;
    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Payment> _payments;

    public PayInEndpoint(IUpvestInvestmentService service, IRepository<Investor> investors, IRepository<Payment> payments)
    {
        _service = service;
        _investors = investors;
        _payments = payments;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PayInRequest request, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(request, http, ct);
            })
            .Produces<PaymentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(PayInRequest request, HttpContext http) => HandleAsync(request, http, default);

    public async Task<IResult> HandleAsync(PayInRequest request, HttpContext http, CancellationToken ct)
    {
        var buyerId = PortfolioEndpointHelpers.GetBuyerId(http);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "amount must be greater than zero." });
        }

        var investor = await _investors.GetByIdAsync(buyerId, ct);
        if (investor is null || investor.UpvestUserId is null)
        {
            return Results.Conflict(new { message = "You are not signed up as an investor." });
        }
        if (string.IsNullOrEmpty(investor.FollowedModelId))
        {
            return Results.Conflict(new { message = "Choose a model portfolio before paying in." });
        }
        // Upvest must have accepted the investor and their account must be ready.
        if (investor.LifecycleStatus != InvestorLifecycleStatus.Active || !investor.AccountReady || investor.AccountGroupId is null)
        {
            return Results.Conflict(new { message = "Your investor account is not ready yet; you cannot pay in." });
        }

        var amount = PortfolioEndpointHelpers.Money(request.Amount);
        var orderKey = Guid.NewGuid();

        // Claim the payment slot before calling Upvest, so a double-submit is refused by the store.
        var payment = new Payment(Guid.NewGuid(), buyerId, amount, orderKey);
        try
        {
            await _payments.AddAsync(payment, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.Conflict(new { message = "A payment is already being processed." });
        }

        var payIn = new InvestorPayIn(
            UserId: investor.UpvestUserId.Value,
            AccountGroupId: investor.AccountGroupId.Value,
            AccountId: investor.AccountId!.Value,
            OwnerName: investor.OwnerName,
            VirtualBankAccountId: investor.VirtualBankAccountId,
            Amount: amount,
            VirtualBankAccountIdempotencyKey: Guid.NewGuid(),
            MoneyInIdempotencyKey: Guid.NewGuid(),
            OrderIdempotencyKey: orderKey);

        try
        {
            var result = await _service.PayInAsync(payIn, ct);

            payment.RecordOrder(result.UpvestOrderId);
            payment.SetStatus(result.Status);
            await _payments.UpdateAsync(payment, ct);

            if (investor.VirtualBankAccountId is null)
            {
                investor.RecordVirtualBankAccount(result.VirtualBankAccountId);
                await _investors.UpdateAsync(investor, ct);
            }

            return Results.Created("api/portfolio/payments", new PaymentResponse
            {
                PaymentId = payment.PaymentId.ToString(),
                Status = result.Status.ToApiString()
            });
        }
        catch (UpvestIntegrationException ex) when (ex.Kind == UpvestFailureKind.Unknown)
        {
            // The outcome is unknown — the order may still have landed. Leave the payment pending for reconciliation.
            return Results.Accepted("api/portfolio/payments", new PaymentResponse
            {
                PaymentId = payment.PaymentId.ToString(),
                Status = PaymentStatus.Pending.ToApiString()
            });
        }
        catch (UpvestIntegrationException)
        {
            // The provider refused the pay-in outright — it did not go through.
            payment.SetStatus(PaymentStatus.Failed);
            await _payments.UpdateAsync(payment, ct);
            throw;
        }
    }
}

/// <summary>
/// GET /api/portfolio/payments — the caller's payments, newest first.
/// </summary>
public class ListPaymentsEndpoint : IEndpoint<IResult, HttpContext>
{
    private readonly IUpvestInvestmentService _service;
    private readonly IRepository<Payment> _payments;

    public ListPaymentsEndpoint(IUpvestInvestmentService service, IRepository<Payment> payments)
    {
        _service = service;
        _payments = payments;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(http, ct);
            })
            .Produces<List<PaymentListItem>>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(HttpContext http) => HandleAsync(http, default);

    public async Task<IResult> HandleAsync(HttpContext http, CancellationToken ct)
    {
        var buyerId = PortfolioEndpointHelpers.GetBuyerId(http);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var payments = await _payments.ListAsync(new PaymentsByBuyerSpecification(buyerId), ct);

        var items = new List<PaymentListItem>(payments.Count);
        foreach (var payment in payments)
        {
            // Reflect what actually happened at Upvest: re-read a still-pending order's status now.
            if (payment.Status == PaymentStatus.Pending && payment.UpvestOrderId is { } orderId)
            {
                try
                {
                    var status = await _service.GetOrderStatusAsync(orderId, ct);
                    if (status != payment.Status)
                    {
                        payment.SetStatus(status);
                        await _payments.UpdateAsync(payment, ct);
                    }
                }
                catch (UpvestIntegrationException)
                {
                    // Keep the last-known status if the re-read is refused/unavailable.
                }
            }

            items.Add(new PaymentListItem
            {
                PaymentId = payment.PaymentId.ToString(),
                Amount = PortfolioEndpointHelpers.Money(payment.Amount),
                Status = payment.Status.ToApiString()
            });
        }

        return Results.Ok(items);
    }
}
