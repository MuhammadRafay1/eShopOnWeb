using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Opts the signed-in shopper in to investing their change (POST api/investing/enrolment),
/// registering them as an investor with Upvest.
/// </summary>
public class EnrolEndpoint : IEndpoint<IResult, EnrolmentRequest, string, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolmentRequest request, ClaimsPrincipal user, IInvestingService investingService) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                return await HandleAsync(request, buyerId, investingService);
            })
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, string buyerId, IInvestingService investingService)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Nationality) ||
            request.Address is null ||
            string.IsNullOrWhiteSpace(request.Address.Line1) ||
            string.IsNullOrWhiteSpace(request.Address.Country))
        {
            return Results.BadRequest("The sign-up form is missing required fields.");
        }

        var registration = new InvestorRegistration(
            request.FirstName,
            request.LastName,
            request.Email,
            request.BirthDate,
            request.Nationality,
            new RegistrationAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber,
            request.TaxId,
            request.TaxCountry);

        EnrolmentView enrolment;
        try
        {
            enrolment = await investingService.EnrolAsync(buyerId, registration);
        }
        catch (ArgumentException ex)
        {
            // Bad country/nationality code and the like.
            return Results.BadRequest(ex.Message);
        }

        var response = new EnrolmentResponse(request.CorrelationId())
        {
            EnrolmentId = enrolment.EnrolmentId,
            Status = InvestingStatusText.Wire(enrolment.Status),
        };
        return Results.Ok(response);
    }
}
