using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>POST /api/investing/enrolment — opt the signed-in shopper in to investing their change.</summary>
public class CreateEnrolmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                EnrolmentRequest request,
                ClaimsPrincipal user,
                IInvestingService investingService,
                CancellationToken cancellationToken) =>
            {
                var buyerId = CallerIdentity.GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                var form = new InvestorEnrolmentForm(
                    request.FirstName, request.LastName, request.Email, request.BirthDate, request.Nationality,
                    request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country,
                    request.PhoneNumber, request.TaxId, request.TaxCountry);

                try
                {
                    var view = await investingService.EnrolAsync(buyerId, form, cancellationToken);
                    return Results.Ok(new EnrolmentResponse { EnrolmentId = view.EnrolmentId, Status = view.Status });
                }
                catch (UpvestApiException ex) when (ex.StatusCode is >= 400 and < 500)
                {
                    // Upvest rejected the sign-up form — the caller can act on it. Do not leak internals.
                    return Results.Problem(statusCode: ex.StatusCode, title: "Enrolment could not be completed.");
                }
                catch (UpvestException)
                {
                    return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Investment provider is unavailable.");
                }
            })
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }
}

/// <summary>GET /api/investing/enrolment — where the caller's enrolment has got to.</summary>
public class GetEnrolmentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/enrolment",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IInvestingService investingService,
                CancellationToken cancellationToken) =>
            {
                var buyerId = CallerIdentity.GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                var view = await investingService.GetEnrolmentAsync(buyerId, cancellationToken);
                return view is null
                    ? Results.NotFound()
                    : Results.Ok(new EnrolmentResponse { EnrolmentId = view.EnrolmentId, Status = view.Status });
            })
            .Produces<EnrolmentResponse>()
            .WithTags("InvestingEndpoints");
    }
}
