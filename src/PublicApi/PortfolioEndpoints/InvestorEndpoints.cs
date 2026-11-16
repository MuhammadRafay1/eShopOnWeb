using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>POST /api/portfolio/investor — sign the signed-in shopper up as an investor.</summary>
public class InvestorSignupEndpoint : IEndpoint<IResult, InvestorSignupRequest, IPortfolioInvestorService>
{
    private readonly IHttpContextAccessor _http;
    public InvestorSignupEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (InvestorSignupRequest request, IPortfolioInvestorService service) => await HandleAsync(request, service))
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(InvestorSignupRequest request, IPortfolioInvestorService service)
    {
        var ctx = _http.HttpContext!;
        var userName = PortfolioHttp.UserName(ctx);
        if (string.IsNullOrEmpty(userName))
        {
            return Results.Unauthorized();
        }
        if (request is null || request.Address is null || request.IdentityDocument is null)
        {
            return Results.BadRequest(new { message = "A complete sign-up form is required." });
        }

        var signup = new InvestorSignup(
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
            new InvestorIdentityDocument(request.IdentityDocument.Type, request.IdentityDocument.Number, request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

        var view = await service.SignUpAsync(userName, signup, ctx.RequestAborted);
        return Results.Ok(new InvestorResponse { InvestorId = view.InvestorId, Status = PortfolioHttp.Status(view.Status) });
    }
}

/// <summary>GET /api/portfolio/investor — where the caller's sign-up has got to.</summary>
public class InvestorStatusEndpoint : IEndpoint<IResult, IPortfolioInvestorService>
{
    private readonly IHttpContextAccessor _http;
    public InvestorStatusEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPortfolioInvestorService service) => await HandleAsync(service))
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IPortfolioInvestorService service)
    {
        var ctx = _http.HttpContext!;
        var userName = PortfolioHttp.UserName(ctx);
        if (string.IsNullOrEmpty(userName))
        {
            return Results.Unauthorized();
        }

        var view = await service.GetStatusAsync(userName, ctx.RequestAborted);
        if (view is null)
        {
            return Results.NotFound(new { message = "You are not signed up as an investor." });
        }
        return Results.Ok(new InvestorResponse { InvestorId = view.InvestorId, Status = PortfolioHttp.Status(view.Status) });
    }
}
