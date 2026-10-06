using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Opts the signed-in shopper in to investing their change (registers them as an investor).</summary>
public class EnrolEndpoint : IEndpoint<IResult, EnrolRequest, EnrolEndpoint.Dependencies>
{
    public record struct Dependencies(
        ClaimsPrincipal User, IInvestingService Investing, CancellationToken CancellationToken);

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                EnrolRequest request,
                ClaimsPrincipal user,
                IInvestingService investing,
                CancellationToken cancellationToken) =>
            {
                return await HandleAsync(request, new Dependencies(user, investing, cancellationToken));
            })
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolRequest request, Dependencies deps)
    {
        var shopperId = deps.User.Identity?.Name;
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }

        if (!DateTimeOffset.TryParse(
                request.BirthDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var birthDate))
        {
            return Results.BadRequest("birthDate must be an ISO-8601 date.");
        }

        var data = new InvestorEnrolmentData
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            BirthDate = birthDate,
            Nationality = request.Nationality,
            Address = new EnrolmentAddress
            {
                Line1 = request.Address.Line1,
                Postcode = request.Address.Postcode,
                City = request.Address.City,
                Country = request.Address.Country
            },
            PhoneNumber = request.PhoneNumber,
            TaxId = request.TaxId,
            TaxCountry = request.TaxCountry
        };

        try
        {
            var investor = await deps.Investing.EnrolAsync(shopperId, data, deps.CancellationToken);
            return Results.Ok(new EnrolmentResponse
            {
                EnrolmentId = investor.EnrolmentId,
                Status = investor.Status.ToText()
            });
        }
        catch (UpvestGatewayException)
        {
            // The provider could not take the shopper on right now. Do not leak provider detail or PII.
            return Results.Problem(
                title: "Could not complete enrolment with the investment provider.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
