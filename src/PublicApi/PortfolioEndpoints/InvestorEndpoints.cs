using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;
using static Microsoft.eShopWeb.PublicApi.PortfolioEndpoints.PortfolioEndpointHelpers;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 1 — sign the signed-in shopper up as an investor with Upvest.</summary>
public class RegisterInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (RegisterInvestorRequest request, ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Unauthenticated();
                }

                var signUp = new InvestorSignUp(
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.BirthDate,
                    request.BirthCity,
                    request.BirthCountry,
                    request.Nationality,
                    new InvestorAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
                    request.PhoneNumber,
                    request.TaxId,
                    request.TaxCountry,
                    new IdentityDocument(request.IdentityDocument.Type, request.IdentityDocument.Number, request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

                var result = await service.RegisterInvestorAsync(buyerId, signUp, ct);
                return ToHttpResult(result, v => new InvestorResponse(v.InvestorId, v.Status), StatusCodes.Status201Created);
            })
            .Produces<InvestorResponse>(StatusCodes.Status201Created)
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>Flow 1 — report where the caller's investor sign-up has got to.</summary>
public class GetInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Unauthenticated();
                }

                var result = await service.GetInvestorAsync(buyerId, ct);
                return ToHttpResult(result, v => new InvestorResponse(v.InvestorId, v.Status));
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
