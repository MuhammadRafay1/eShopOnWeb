using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Opts the signed-in shopper in to investing their spare change, registering them with Upvest.
/// </summary>
public class EnrolmentCreateEndpoint : IEndpoint<IResult, EnrolmentRequest, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolmentRequest request, HttpContext context) => await HandleAsync(request, context))
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, HttpContext context)
    {
        var shopperId = context.User.Identity?.Name;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var investingService = context.RequestServices.GetRequiredService<IInvestingService>();

        var details = new InvestorEnrolmentDetails(
            request.FirstName,
            request.LastName,
            request.Email,
            request.BirthDate,
            request.Nationality,
            new EnrolmentAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber,
            request.TaxId,
            request.TaxCountry);

        var investor = await investingService.EnrolAsync(shopperId, details, CancellationToken.None);

        var response = new EnrolmentResponse(request.CorrelationId())
        {
            EnrolmentId = investor.PublicId,
            Status = investor.Status.ToWire(),
        };
        return Results.Ok(response);
    }
}
