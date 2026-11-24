using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public record CreatePaymentCommand(string ShopperId, decimal Amount);

/// <summary>
/// Flow 3 — the signed-in investor pays an amount (in euros) in, invested across their model's funds.
/// </summary>
public class CreatePaymentEndpoint : IEndpoint<IResult, CreatePaymentCommand, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentRequest request, ClaimsPrincipal user, IPortfolioService service) =>
            {
                var shopperId = user.GetShopperId();
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                return await HandleAsync(new CreatePaymentCommand(shopperId, request?.Amount ?? 0m), service);
            })
            .Produces<CreatePaymentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CreatePaymentCommand command, IPortfolioService service)
    {
        var view = await service.PayInAsync(command.ShopperId, command.Amount);
        return Results.Ok(new CreatePaymentResponse { PaymentId = view.PaymentId, Status = view.Status });
    }
}
