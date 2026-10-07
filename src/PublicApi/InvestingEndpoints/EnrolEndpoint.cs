using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Opts the signed-in shopper in to investing their change, registering them as an investor with Upvest.</summary>
public class EnrolEndpoint : IEndpoint<IResult, EnrolmentRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolmentRequest request, ClaimsPrincipal user, IInvestingService investing) =>
            {
                request.BuyerId = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
                return await HandleAsync(request, investing);
            })
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Nationality) ||
            request.Address is null ||
            string.IsNullOrWhiteSpace(request.Address.Line1) ||
            string.IsNullOrWhiteSpace(request.Address.Postcode) ||
            string.IsNullOrWhiteSpace(request.Address.City) ||
            string.IsNullOrWhiteSpace(request.Address.Country) ||
            string.IsNullOrWhiteSpace(request.TaxId) ||
            string.IsNullOrWhiteSpace(request.TaxCountry) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return Results.Problem(detail: "The sign-up form is incomplete.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!DateOnly.TryParse(request.BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
        {
            return Results.Problem(detail: "birthDate must be an ISO-8601 date (yyyy-MM-dd).", statusCode: StatusCodes.Status400BadRequest);
        }

        var details = new InvestorEnrolmentDetails(
            request.FirstName,
            request.LastName,
            request.Email,
            birthDate,
            request.Nationality,
            new EnrolmentAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber,
            request.TaxId,
            request.TaxCountry);

        try
        {
            var investor = await investing.EnrolAsync(request.BuyerId, details);
            var response = new EnrolmentResponse(request.CorrelationId())
            {
                EnrolmentId = investor.Id,
                Status = investor.Status.ToApiString()
            };
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            // e.g. an unrecognised nationality/country code.
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception)
        {
            // Do not leak provider error detail (which could carry personal data).
            return Results.Problem(detail: "Could not register the shopper with the investment provider.", statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
