using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 3 — the caller's payments, newest first, each with its amount and current status at Upvest.
/// </summary>
public class GetPaymentsEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public GetPaymentsEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            () => await HandleAsync())
            .Produces<PaymentsResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        var shopperId = ShopperId;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var payments = Service<IRepository<Payment>>();
        var upvest = Service<IUpvestService>();

        var list = await payments.ListAsync(new PaymentsByShopperSpecification(shopperId), Ct);

        var items = new List<PaymentItemDto>(list.Count);
        foreach (var payment in list)
        {
            // Refresh any still-pending payment that has reached Upvest, so the status is current.
            if (payment.Status == PaymentStatus.Pending && payment.UpvestPortfolioOrderId is not null)
            {
                var refreshed = await upvest.GetPaymentStatusAsync(payment.UpvestPortfolioOrderId, Ct);
                if (refreshed.Determined && refreshed.Status != payment.Status)
                {
                    payment.SetStatus(refreshed.Status, refreshed.Detail);
                    await payments.UpdateAsync(payment, Ct);
                }
            }

            items.Add(new PaymentItemDto
            {
                PaymentId = payment.Id,
                Amount = payment.Amount,
                Status = payment.Status.ToString().ToLowerInvariant()
            });
        }

        return Results.Ok(new PaymentsResponse(System.Guid.NewGuid()) { Payments = items });
    }
}
