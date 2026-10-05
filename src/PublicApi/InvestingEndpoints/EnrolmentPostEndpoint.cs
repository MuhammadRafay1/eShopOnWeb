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
/// Opts the signed-in shopper in to investing their change, registering them as
/// an investor with Upvest.
/// </summary>
public class EnrolmentPostEndpoint : IEndpoint<IResult, CreateEnrolmentRequest, IEnrolmentService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EnrolmentPostEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateEnrolmentRequest request, IEnrolmentService enrolmentService) =>
            {
                return await HandleAsync(request, enrolmentService);
            })
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateEnrolmentRequest request, IEnrolmentService enrolmentService)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var ct = httpContext.RequestAborted;
        var buyerId = httpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var details = new EnrolmentDetails(
            request.FirstName,
            request.LastName,
            request.Email,
            request.BirthDate,
            request.Nationality,
            request.Address.Line1,
            request.Address.Postcode,
            request.Address.City,
            request.Address.Country,
            request.PhoneNumber,
            request.TaxId,
            request.TaxCountry);

        var investor = await enrolmentService.EnrolAsync(buyerId, details, ct);

        return Results.Ok(new EnrolmentResponse(request.CorrelationId())
        {
            EnrolmentId = investor.Id,
            Status = investor.Status.ToString().ToLowerInvariant()
        });
    }
}
