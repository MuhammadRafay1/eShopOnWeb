using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 1 — sign the signed-in shopper up as an investor with Upvest.</summary>
public class RegisterInvestorEndpoint : IEndpoint<IResult, RegisterInvestorRequest, IInvestorService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (RegisterInvestorRequest request, IInvestorService service, HttpContext http, CancellationToken ct) =>
            {
                request.CallerId = http.User.ShopperId();
                request.Cancellation = ct;
                return await HandleAsync(request, service);
            })
            .Produces<InvestorResponse>(StatusCodes.Status201Created)
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(RegisterInvestorRequest request, IInvestorService service) =>
        EndpointSupport.Guarded(async () =>
        {
            var registration = new InvestorRegistration(
                request.FirstName,
                request.LastName,
                request.Email,
                request.BirthDate,
                request.BirthCity,
                request.BirthCountry ?? string.Empty,
                request.Nationality,
                new AddressInput(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
                request.PhoneNumber,
                request.TaxId,
                request.TaxCountry,
                new IdentityDocumentInput(request.IdentityDocument.Type, request.IdentityDocument.Number, request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

            var result = await service.RegisterAsync(request.CallerId, registration, request.Cancellation);
            return Results.Created("api/portfolio/investor", new InvestorResponse
            {
                InvestorId = result.InvestorId,
                Status = StatusText.For(result.State),
            });
        });
}

/// <summary>Flow 1 — where the caller's sign-up has got to.</summary>
public class GetInvestorEndpoint : IEndpoint<IResult, GetInvestorRequest, IInvestorService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestorService service, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(new GetInvestorRequest { CallerId = http.User.ShopperId(), Cancellation = ct }, service);
            })
            .Produces<InvestorResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(GetInvestorRequest request, IInvestorService service) =>
        EndpointSupport.Guarded(async () =>
        {
            var result = await service.GetAsync(request.CallerId, request.Cancellation);
            if (result is null)
                return Results.NotFound();
            return Results.Ok(new InvestorResponse
            {
                InvestorId = result.InvestorId,
                Status = StatusText.For(result.State),
            });
        });
}
