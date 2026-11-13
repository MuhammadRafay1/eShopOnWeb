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
/// Flow 3 — the signed-in investor pays an amount in; it is credited and invested across their model.
/// A shopper who is not an accepted investor with a model chosen is refused.
/// </summary>
public class CreatePaymentEndpoint : PortfolioEndpointBase, IEndpoint<IResult, CreatePaymentRequest>
{
    public CreatePaymentEndpoint(IHttpContextAccessor accessor) : base(accessor) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentRequest request) => await HandleAsync(request))
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CreatePaymentRequest request)
    {
        var shopperId = ShopperId;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        if (request.Amount <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Amount)] = new[] { "amount must be greater than zero." }
            });
        }
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency!.Trim().ToUpperInvariant();

        var payments = Service<IRepository<Payment>>();
        var investors = Service<IReadRepository<Investor>>();
        var upvest = Service<IUpvestService>();

        var investor = await investors.FirstOrDefaultAsync(new InvestorByShopperSpecification(shopperId), Ct);
        if (investor is null)
        {
            return Results.Conflict(new { detail = "You are not registered as an investor." });
        }
        if (investor.Status != InvestorStatus.Active)
        {
            return Results.Conflict(new { detail = "Your investor account is not yet active at Upvest; you cannot pay in until it is ready." });
        }
        if (investor.SelectedModelId is null)
        {
            return Results.Conflict(new { detail = "Choose a model portfolio before paying in." });
        }
        if (investor.UpvestAccountGroupId is null || investor.UpvestAccountId is null)
        {
            return Results.Conflict(new { detail = "Your Upvest account is not fully provisioned yet." });
        }

        var payment = new Payment(shopperId, request.Amount, currency);
        await payments.AddAsync(payment, Ct);

        var result = await upvest.PayInAsync(investor.UpvestAccountGroupId, investor.UpvestUserId, investor.UpvestAccountId, request.Amount, currency, Ct);
        if (result.TopupId is not null) payment.LinkTopup(result.TopupId);
        if (result.PortfolioOrderId is not null) payment.LinkPortfolioOrder(result.PortfolioOrderId);
        payment.SetStatus(result.Status, result.Detail);
        await payments.UpdateAsync(payment, Ct);

        return Results.Ok(new PaymentResponse(request.CorrelationId())
        {
            PaymentId = payment.Id,
            Status = payment.Status.ToString().ToLowerInvariant(),
            Detail = result.Detail
        });
    }
}
