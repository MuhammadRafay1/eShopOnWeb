using System;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Opt the signed-in shopper in to investing their change (Flow 1).</summary>
public class EnrolmentPostEndpoint : IEndpoint<IResult, EnrolmentRequest, ClaimsPrincipal>
{
    // Endpoints are resolved as singletons, so scoped services (InvestingService -> DbContext) are
    // resolved per request from a fresh scope rather than captured in the constructor.
    private readonly IServiceScopeFactory _scopeFactory;

    public EnrolmentPostEndpoint(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (EnrolmentRequest request, ClaimsPrincipal user) => await HandleAsync(request, user))
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, ClaimsPrincipal user)
    {
        var shopperId = user.ShopperId();
        if (string.IsNullOrEmpty(shopperId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Nationality) ||
            string.IsNullOrWhiteSpace(request.TaxId) || string.IsNullOrWhiteSpace(request.TaxCountry) ||
            request.Address is null)
        {
            return Results.BadRequest(new { error = "firstName, lastName, email, birthDate, nationality, address, taxId and taxCountry are required." });
        }

        if (!DateTimeOffset.TryParse(request.BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var birthDate))
            return Results.BadRequest(new { error = "birthDate must be an ISO-8601 date." });

        var details = new UpvestInvestorDetails(
            request.FirstName, request.LastName, request.Email, birthDate, request.Nationality,
            request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country,
            request.PhoneNumber, request.TaxId, request.TaxCountry,
            ConsentTimestamp: default); // set to a stable per-enrolment value by InvestingService

        using var scope = _scopeFactory.CreateScope();
        var investing = scope.ServiceProvider.GetRequiredService<InvestingService>();
        try
        {
            var enrolment = await investing.EnrolAsync(shopperId, details, default);
            return Results.Ok(new EnrolmentResponse { EnrolmentId = enrolment.PublicId, Status = enrolment.Status.ToWire() });
        }
        catch (UpvestGatewayException ex) when (ex.IsTransient)
        {
            // Upvest is temporarily unavailable; the shopper can retry the opt-in.
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
