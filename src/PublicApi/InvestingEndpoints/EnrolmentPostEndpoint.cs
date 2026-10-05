using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Opts the signed-in shopper in to investing their change (Flow 1).</summary>
public class EnrolmentPostEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                EnrolmentRequest request, HttpContext http, IInvestingService investing) =>
            {
                var shopperId = http.User.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

                var form = request.ToForm(out var error);
                if (form is null) return Results.BadRequest(new { error });

                try
                {
                    var investor = await investing.EnrolAsync(shopperId, form, http.RequestAborted);
                    return Results.Ok(new EnrolmentResponse
                    {
                        EnrolmentId = investor.Id,
                        Status = InvestingHttp.ToWire(investor.Status)
                    });
                }
                catch (UpvestIntegrationException ex)
                {
                    return InvestingHttp.Problem(ex);
                }
            })
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags(InvestingHttp.Tag);
    }
}
