using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 1 — signs the signed-in shopper up as an investor with Upvest.
/// POST /api/portfolio/investor
/// </summary>
public class RegisterInvestorEndpoint : IEndpoint<IResult, RegisterInvestorRequest, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (RegisterInvestorRequest request, ClaimsPrincipal user, IPortfolioService service) =>
            {
                return await HandleAsync(request, user.BuyerId(), service);
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(RegisterInvestorRequest request, string buyerId, IPortfolioService service)
    {
        var ct = CancellationToken.None;
        var registration = new InvestorRegistration(
            FirstName: request.FirstName,
            LastName: request.LastName,
            Email: request.Email,
            BirthDate: request.BirthDate,
            BirthCity: request.BirthCity,
            BirthCountry: request.BirthCountry,
            Nationality: request.Nationality,
            Address: new InvestorAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            PhoneNumber: request.PhoneNumber,
            TaxId: request.TaxId,
            TaxCountry: request.TaxCountry,
            IdentityDocument: new InvestorIdentityDocument(
                request.IdentityDocument.Type, request.IdentityDocument.Number,
                request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

        var view = await service.RegisterInvestorAsync(buyerId, registration, ct);
        return Results.Created("api/portfolio/investor", new InvestorResponse(view.InvestorId, view.Status));
    }
}
