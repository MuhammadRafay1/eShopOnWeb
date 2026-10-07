using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/investing/enrolment — opt the signed-in shopper in to investing their change, submitting
/// the investor sign-up form to Upvest.
/// </summary>
public class EnrolmentCreateEndpoint : IEndpoint<IResult, EnrolmentRequest, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolmentRequest request, HttpContext http) => await HandleAsync(request, http))
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, HttpContext http)
    {
        var buyerId = http.User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investing = http.RequestServices.GetRequiredService<IInvestingService>();

        var form = new InvestorSignUpForm
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            BirthDate = request.BirthDate,
            Nationality = request.Nationality,
            PhoneNumber = request.PhoneNumber,
            TaxId = request.TaxId,
            TaxCountry = request.TaxCountry,
            Address = new InvestorAddress
            {
                Line1 = request.Address.Line1,
                Postcode = request.Address.Postcode,
                City = request.Address.City,
                Country = request.Address.Country
            }
        };

        var investor = await investing.EnrolAsync(buyerId, form, http.RequestAborted);

        var response = new EnrolmentResponse(request.CorrelationId())
        {
            EnrolmentId = investor.EnrolmentId,
            Status = investor.Status.ToText()
        };
        return Results.Ok(response);
    }
}
