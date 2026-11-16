using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using MinimalApi.Endpoint;
using static Microsoft.eShopWeb.PublicApi.PortfolioEndpoints.PortfolioEndpointSupport;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>POST /api/portfolio/investor — sign the signed-in shopper up as an investor with Upvest.</summary>
public class SignUpInvestorEndpoint : IEndpoint<IResult, SignUpInvestorRequest, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public SignUpInvestorEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (SignUpInvestorRequest request, IUpvestPortfolioService service) => await HandleAsync(request, service))
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(SignUpInvestorRequest request, IUpvestPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var shopper = ResolveShopper(ctx.User);
        if (shopper is null) return Results.Unauthorized();

        if (request is null || request.Address is null || request.IdentityDocument is null)
        {
            return Results.BadRequest(new { error = "A complete sign-up form, including address and identityDocument, is required." });
        }

        var form = new InvestorSignupForm(
            request.FirstName, request.LastName, request.Email, request.BirthDate,
            request.BirthCity, request.BirthCountry, request.Nationality,
            new InvestorAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber, request.TaxId, request.TaxCountry,
            new IdentityDocumentForm(request.IdentityDocument.Type, request.IdentityDocument.Number,
                request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

        try
        {
            var result = await service.SignUpInvestorAsync(shopper, form, ctx.RequestAborted);
            return Results.Ok(new InvestorResponse
            {
                InvestorId = result.InvestorId,
                Status = PortfolioStatusText.For(result.Status)
            });
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }
}

/// <summary>GET /api/portfolio/investor — the caller's sign-up status.</summary>
public class GetInvestorEndpoint : IEndpoint<IResult, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public GetInvestorEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IUpvestPortfolioService service) => await HandleAsync(service))
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IUpvestPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var shopper = ResolveShopper(ctx.User);
        if (shopper is null) return Results.Unauthorized();

        try
        {
            var result = await service.GetInvestorStatusAsync(shopper, ctx.RequestAborted);
            if (result is null) return Results.NotFound(new { error = "The caller is not registered as an investor." });

            return Results.Ok(new InvestorResponse
            {
                InvestorId = result.InvestorId,
                Status = PortfolioStatusText.For(result.Status)
            });
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }
}

/// <summary>PUT /api/portfolio/investor/model — the investor chooses which model their portfolio follows.</summary>
public class ChooseModelEndpoint : IEndpoint<IResult, ChooseModelRequest, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public ChooseModelEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ChooseModelRequest request, IUpvestPortfolioService service) => await HandleAsync(request, service))
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ChooseModelRequest request, IUpvestPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var shopper = ResolveShopper(ctx.User);
        if (shopper is null) return Results.Unauthorized();

        if (request is null || string.IsNullOrWhiteSpace(request.ModelId))
        {
            return Results.BadRequest(new { error = "A modelId is required." });
        }

        try
        {
            await service.ChooseModelAsync(shopper, request.ModelId, ctx.RequestAborted);
            return Results.Ok(new { modelId = request.ModelId });
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }
}
