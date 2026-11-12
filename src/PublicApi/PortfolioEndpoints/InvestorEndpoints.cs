using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// POST /api/portfolio/investor — sign the signed-in shopper up as an investor at Upvest.
/// </summary>
public class BecomeInvestorEndpoint : IEndpoint<IResult, InvestorSignupRequest, HttpContext>
{
    private readonly IUpvestInvestmentService _service;
    private readonly IRepository<Investor> _investors;

    public BecomeInvestorEndpoint(IUpvestInvestmentService service, IRepository<Investor> investors)
    {
        _service = service;
        _investors = investors;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (InvestorSignupRequest request, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(request, http, ct);
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(InvestorSignupRequest request, HttpContext http) => HandleAsync(request, http, default);

    public async Task<IResult> HandleAsync(InvestorSignupRequest request, HttpContext http, CancellationToken ct)
    {
        var buyerId = PortfolioEndpointHelpers.GetBuyerId(http);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        if (!DateOnly.TryParse(request.BirthDate, out var birthDate))
        {
            return Results.BadRequest(new { message = "birthDate must be an ISO-8601 date (YYYY-MM-DD)." });
        }
        if (!DateOnly.TryParse(request.IdentityDocument.ExpiryDate, out var expiryDate))
        {
            return Results.BadRequest(new { message = "identityDocument.expiryDate must be an ISO-8601 date (YYYY-MM-DD)." });
        }

        var ownerName = BuildOwnerName(request.FirstName, request.LastName);

        var signup = new InvestorSignup(
            FirstName: request.FirstName,
            LastName: request.LastName,
            Email: request.Email,
            BirthDate: birthDate,
            BirthCity: request.BirthCity,
            BirthCountry: request.BirthCountry,
            Nationality: request.Nationality,
            Address: new InvestorAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            PhoneNumber: request.PhoneNumber,
            TaxId: request.TaxId,
            TaxCountry: request.TaxCountry,
            IdentityDocument: new IdentityDocumentInput(request.IdentityDocument.Type, request.IdentityDocument.Number, request.IdentityDocument.IssuingCountry, expiryDate));

        // Claim the shopper's investor slot before calling Upvest, so a concurrent double-submit is refused by the store.
        var investor = await _investors.GetByIdAsync(buyerId, ct);
        if (investor is null)
        {
            investor = new Investor(buyerId, Guid.NewGuid(), ownerName);
            try
            {
                await _investors.AddAsync(investor, ct);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // A concurrent request already claimed this shopper's slot.
                return Results.Conflict(new { message = "A sign-up for this shopper is already in progress." });
            }
        }
        else if (investor.UpvestUserId is not null)
        {
            return Results.Conflict(new { message = "This shopper is already an investor." });
        }

        // Provision (new, or reconcile an earlier incomplete attempt) using the stored idempotency key.
        var result = await _service.ProvisionInvestorAsync(signup, investor.SignupIdempotencyKey, ct);

        investor.RecordUser(result.UpvestUserId);
        if (result.AccountGroupId is { } groupId) investor.RecordAccountGroup(groupId);
        if (result.AccountId is { } accountId) investor.RecordAccount(accountId);
        investor.SetLifecycleStatus(result.Status);
        await _investors.UpdateAsync(investor, ct);

        var response = new InvestorResponse
        {
            InvestorId = result.UpvestUserId.ToString(),
            Status = result.Status.ToApiString()
        };
        return Results.Created("api/portfolio/investor", response);
    }

    private static string BuildOwnerName(string firstName, string lastName)
    {
        var raw = $"{firstName} {lastName}".Trim();
        // Keep within the provider's counterparty-name character set; never used in logs.
        var cleaned = Regex.Replace(raw, "[^0-9A-Za-z+?/:()\\.,'; -]", " ").Trim();
        return string.IsNullOrEmpty(cleaned) ? "eShop Investor" : cleaned;
    }
}

/// <summary>
/// GET /api/portfolio/investor — where the caller's sign-up has got to (re-read from Upvest).
/// </summary>
public class GetInvestorStatusEndpoint : IEndpoint<IResult, HttpContext>
{
    private readonly IUpvestInvestmentService _service;
    private readonly IRepository<Investor> _investors;

    public GetInvestorStatusEndpoint(IUpvestInvestmentService service, IRepository<Investor> investors)
    {
        _service = service;
        _investors = investors;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(http, ct);
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(HttpContext http) => HandleAsync(http, default);

    public async Task<IResult> HandleAsync(HttpContext http, CancellationToken ct)
    {
        var buyerId = PortfolioEndpointHelpers.GetBuyerId(http);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investor = await _investors.GetByIdAsync(buyerId, ct);
        if (investor is null || investor.UpvestUserId is null)
        {
            return Results.NotFound(new { message = "You are not signed up as an investor." });
        }

        // Reflect what actually happened at Upvest by re-reading the user's status now; fall back to the
        // last-known status if the provider refuses the read (e.g. a scope the sandbox client lacks).
        var status = investor.LifecycleStatus;
        try
        {
            status = await _service.GetInvestorStatusAsync(investor.UpvestUserId.Value, investor.AccountReady, ct);
            if (status != investor.LifecycleStatus)
            {
                investor.SetLifecycleStatus(status);
                await _investors.UpdateAsync(investor, ct);
            }
        }
        catch (UpvestIntegrationException)
        {
            // Keep the last-known status when the live re-read cannot be performed.
        }

        var response = new InvestorResponse
        {
            InvestorId = investor.UpvestUserId.Value.ToString(),
            Status = status.ToApiString()
        };
        return Results.Ok(response);
    }
}
