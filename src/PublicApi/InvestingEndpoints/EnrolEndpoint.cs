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
/// Opts the signed-in shopper in to investing their change, submitting them to Upvest as an investor.
/// </summary>
public class EnrolEndpoint : IEndpoint<IResult, EnrolInvestingRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EnrolEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (EnrolInvestingRequest request, IInvestingService investingService) =>
            {
                return await HandleAsync(request, investingService);
            })
            .Produces<EnrolInvestingResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EnrolInvestingRequest request, IInvestingService investingService)
    {
        var response = new EnrolInvestingResponse(request.CorrelationId());

        var buyerId = _httpContextAccessor.HttpContext?.User.GetBuyerId();
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        var form = new InvestorSignUp
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

        var result = await investingService.EnrolAsync(buyerId, form);
        response.EnrolmentId = result.EnrolmentId;
        response.Status = result.Status.ToText();
        return Results.Ok(response);
    }
}
