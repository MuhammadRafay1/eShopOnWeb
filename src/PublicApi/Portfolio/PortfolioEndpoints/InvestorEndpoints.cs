using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.PortfolioEndpoints;

/// <summary>POST /api/portfolio/investor — sign the signed-in shopper up as an investor.</summary>
public class InvestorSignupEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (InvestorSignupRequest request, ClaimsPrincipal user, InvestorService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async shopperId =>
                {
                    var cmd = new InvestorSignupCommand(
                        request.FirstName, request.LastName, request.Email, request.BirthDate,
                        request.BirthCity, request.BirthCountry, request.Nationality,
                        request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country,
                        request.PhoneNumber, request.TaxId, request.TaxCountry,
                        request.IdentityDocument.Type, request.IdentityDocument.Number,
                        request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate);

                    var result = await service.SignUpAsync(shopperId, cmd, ct);
                    return Results.Ok(new InvestorResponse { InvestorId = result.InvestorId?.ToString(), Status = result.Status });
                }))
            .Produces<InvestorResponse>()
            .WithTags(EndpointSupport.Tag);
    }
}

/// <summary>GET /api/portfolio/investor — where the caller's sign-up has got to.</summary>
public class InvestorStatusEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, InvestorService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async shopperId =>
                {
                    var result = await service.GetStatusAsync(shopperId, ct);
                    if (result is null)
                        return Results.NotFound();
                    return Results.Ok(new InvestorResponse { InvestorId = result.InvestorId?.ToString(), Status = result.Status });
                }))
            .Produces<InvestorResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags(EndpointSupport.Tag);
    }
}

/// <summary>PUT /api/portfolio/investor/model — the signed-in investor chooses which model to follow.</summary>
public class ChooseModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ChooseModelRequest request, ClaimsPrincipal user, InvestorService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async shopperId =>
                {
                    await service.ChooseModelAsync(shopperId, request.ModelId, ct);
                    return Results.Ok(new { modelId = request.ModelId });
                }))
            .WithTags(EndpointSupport.Tag);
    }
}
