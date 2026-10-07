using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST api/investing/enrolment — opt the signed-in shopper in to investing their
/// change. Idempotent per shopper.
/// </summary>
public class EnrolEndpoint : IEndpoint<IResult, EnrolmentRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EnrolEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolmentRequest request, IInvestingService investingService, CancellationToken ct) =>
                await HandleAsync(request, investingService, ct))
            .Produces<EnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolmentRequest request, IInvestingService investingService)
        => await HandleAsync(request, investingService, CancellationToken.None);

    private async Task<IResult> HandleAsync(EnrolmentRequest request, IInvestingService investingService, CancellationToken ct)
    {
        var buyerId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        if (request.Address is null)
            return Results.BadRequest("An address is required.");
        if (!DateOnly.TryParse(request.BirthDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            return Results.BadRequest("birthDate must be an ISO-8601 date, e.g. 1990-01-31.");

        var details = new InvestorDetails(
            request.FirstName,
            request.LastName,
            request.Email,
            birthDate,
            request.Nationality,
            new InvestorAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber,
            request.TaxId,
            request.TaxCountry);

        var view = await investingService.EnrolAsync(buyerId, details, ct);

        return Results.Ok(new EnrolmentResponse(request.CorrelationId())
        {
            EnrolmentId = view.EnrolmentId,
            Status = view.Status
        });
    }
}
