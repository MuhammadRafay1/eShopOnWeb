using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>POST /api/portfolio/investor — sign the signed-in shopper up as an investor at Upvest.</summary>
public class SignUpInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (InvestorSignupRequest request, HttpContext http, IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var shopper = PortfolioUser.Name(http.User);
                var input = new InvestorSignupInput(
                    request.FirstName, request.LastName, request.Email, request.BirthDate,
                    request.BirthCity, request.BirthCountry, request.Nationality,
                    new AddressInput(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
                    request.PhoneNumber, request.TaxId, request.TaxCountry,
                    new IdentityDocumentInput(request.IdentityDocument.Type, request.IdentityDocument.Number,
                        request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

                var result = await svc.SignUpInvestorAsync(shopper, input, ct);
                return Results.Ok(new InvestorResponse(result.InvestorId, result.Status));
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>GET /api/portfolio/investor — where the caller's sign-up has got to.</summary>
public class GetInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var shopper = PortfolioUser.Name(http.User);
                var result = await svc.GetInvestorAsync(shopper, ct);
                return result is null
                    ? Results.NotFound()
                    : Results.Ok(new InvestorResponse(result.InvestorId, result.Status));
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
