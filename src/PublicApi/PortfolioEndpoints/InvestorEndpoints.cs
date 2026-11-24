using System.Linq;
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

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>POST /api/portfolio/investor — sign the signed-in shopper up as an investor.</summary>
public class SignUpInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SignUpInvestorRequest request, ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var unauthorized = user.RequireUserName(out var userName);
                if (unauthorized != null) return unauthorized;

                var validation = ValidateSignUp(request);
                if (validation != null) return validation;

                var signUp = new InvestorSignUp(
                    request.FirstName, request.LastName, request.Email, request.BirthDate,
                    request.BirthCity, request.BirthCountry, request.Nationality,
                    new InvestorAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
                    request.PhoneNumber, request.TaxId, request.TaxCountry,
                    new InvestorIdentityDocument(request.IdentityDocument.Type, request.IdentityDocument.Number,
                        request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

                var result = await service.SignUpInvestorAsync(userName, signUp, ct);
                return Results.Created("api/portfolio/investor", result.ToResponse());
            })
            .Produces<InvestorResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("PortfolioEndpoints");
    }

    private static IResult? ValidateSignUp(SignUpInvestorRequest r)
    {
        var missing = new[]
        {
            ("firstName", r.FirstName), ("lastName", r.LastName), ("birthDate", r.BirthDate),
            ("nationality", r.Nationality), ("address.line1", r.Address?.Line1 ?? ""),
            ("address.postcode", r.Address?.Postcode ?? ""), ("address.city", r.Address?.City ?? ""),
            ("address.country", r.Address?.Country ?? ""),
        }.Where(f => string.IsNullOrWhiteSpace(f.Item2)).Select(f => f.Item1).ToArray();

        return missing.Length == 0 ? null : Results.BadRequest(new { message = $"Missing required field(s): {string.Join(", ", missing)}." });
    }
}

/// <summary>GET /api/portfolio/investor — the caller's sign-up status.</summary>
public class GetInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var unauthorized = user.RequireUserName(out var userName);
                if (unauthorized != null) return unauthorized;

                var result = await service.GetInvestorAsync(userName, ct);
                return result is null ? Results.NotFound() : Results.Ok(result.ToResponse());
            })
            .Produces<InvestorResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>PUT /api/portfolio/investor/model — the investor chooses which model to follow.</summary>
public class SelectModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SelectModelRequest request, ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var unauthorized = user.RequireUserName(out var userName);
                if (unauthorized != null) return unauthorized;
                if (string.IsNullOrWhiteSpace(request.ModelId))
                    return Results.BadRequest(new { message = "modelId is required." });

                await service.SelectModelAsync(userName, request.ModelId, ct);
                return Results.Ok(new { modelId = request.ModelId });
            })
            .WithTags("PortfolioEndpoints");
    }
}
