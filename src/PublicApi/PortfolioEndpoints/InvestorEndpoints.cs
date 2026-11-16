using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 1 — sign the signed-in shopper up as an investor with Upvest.</summary>
public sealed class RegisterInvestorEndpoint : PortfolioEndpointBase, IEndpoint<IResult, RegisterInvestorRequest>
{
    public RegisterInvestorEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<RegisterInvestorEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (RegisterInvestorRequest request) => await HandleAsync(request))
            .Produces<InvestorResponse>()
            .Produces<InvestorResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(RegisterInvestorRequest request)
    {
        var buyerId = BuyerId;
        if (buyerId is null)
        {
            return Results.Unauthorized();
        }

        if (!Validate(request, out var birthDate, out var expiryDate, out var error))
        {
            return Results.BadRequest(new { error });
        }

        // Atomic claim: one investor per shopper. A second concurrent sign-up is refused by the store.
        var investor = new Investor(buyerId);
        if (!Store.TryCreateInvestor(investor))
        {
            var existing = Store.GetInvestor(buyerId)!;
            return await DescribeAsync(existing, StatusCodes.Status200OK);
        }

        var form = new InvestorSignUp(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email.Trim(),
            birthDate,
            request.BirthCity.Trim(),
            request.BirthCountry.Trim().ToUpperInvariant(),
            request.Nationality.Trim().ToUpperInvariant(),
            new SignUpAddress(request.Address.Line1.Trim(), request.Address.Postcode.Trim(), request.Address.City.Trim(), request.Address.Country.Trim().ToUpperInvariant()),
            request.PhoneNumber?.Trim() ?? string.Empty,
            request.TaxId.Trim(),
            request.TaxCountry.Trim().ToUpperInvariant(),
            new SignUpIdentityDocument(
                request.IdentityDocument.Type.Trim().ToUpperInvariant(),
                request.IdentityDocument.Number.Trim(),
                request.IdentityDocument.IssuingCountry.Trim().ToUpperInvariant(),
                expiryDate));

        var keys = new InvestorOnboardingKeys(
            investor.UserIdempotencyKey, investor.TaxIdempotencyKey,
            investor.AccountGroupIdempotencyKey, investor.AccountIdempotencyKey);

        InvestorOnboardingResult result;
        try
        {
            result = await Gateway.OnboardInvestorAsync(form, keys, Ct);
        }
        catch (UpvestGatewayException ex)
        {
            // Onboarding failed before the investor was established. Release the claim unless the outcome is
            // unknown (then keep the record so its persisted idempotency keys can reconcile a replay later).
            if (!ex.IsOutcomeUnknown)
            {
                Store.RemoveInvestor(buyerId);
            }

            Logger.LogWarning("Investor onboarding failed for a shopper (status {Status}).", ex.StatusCode);
            return PortfolioShared.ToProblem(ex);
        }

        investor.UpvestUserId = result.UserId;
        investor.CheckId = result.CheckId;
        investor.AccountGroupId = result.AccountGroupId;
        investor.AccountId = result.AccountId;
        Store.UpdateInvestor(investor);

        // The investor now exists at Upvest; the status read-back is best-effort (a scope-limited environment
        // cannot read it) and must never undo the successful creation.
        var status = await ReadStatusAsync(result.UserId, result.AccountId);
        return Results.Created("api/portfolio/investor",
            new InvestorResponse { InvestorId = result.UserId.ToString(), Status = PortfolioShared.ToWire(status) });
    }

    private async Task<IResult> DescribeAsync(Investor investor, int statusCode)
    {
        if (investor.UpvestUserId is null)
        {
            // Claimed but the Upvest user id was never captured (a prior unknown outcome).
            return Results.Json(
                new InvestorResponse { InvestorId = string.Empty, Status = PortfolioShared.ToWire(InvestorStatus.Pending) },
                statusCode: statusCode);
        }

        var status = await ReadStatusAsync(investor.UpvestUserId.Value, investor.AccountId);
        return Results.Json(
            new InvestorResponse { InvestorId = investor.UpvestUserId.Value.ToString(), Status = PortfolioShared.ToWire(status) },
            statusCode: statusCode);
    }

    private async Task<InvestorStatus> ReadStatusAsync(Guid userId, Guid? accountId)
    {
        try
        {
            return await PortfolioShared.ResolveInvestorStatusAsync(Gateway, userId, accountId, Ct);
        }
        catch (UpvestGatewayException ex)
        {
            Logger.LogWarning("Investor status read-back unavailable (status {Status}); reporting pending.", ex.StatusCode);
            return InvestorStatus.Pending;
        }
    }

    private static bool Validate(RegisterInvestorRequest r, out DateTimeOffset birthDate, out DateTimeOffset expiryDate, out string error)
    {
        birthDate = default;
        expiryDate = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName)) { error = "firstName and lastName are required."; return false; }
        if (string.IsNullOrWhiteSpace(r.Email)) { error = "email is required."; return false; }
        if (r.Address is null || string.IsNullOrWhiteSpace(r.Address.Line1) || string.IsNullOrWhiteSpace(r.Address.Postcode)
            || string.IsNullOrWhiteSpace(r.Address.City) || string.IsNullOrWhiteSpace(r.Address.Country)) { error = "a complete address is required."; return false; }
        if (string.IsNullOrWhiteSpace(r.Nationality)) { error = "nationality is required."; return false; }
        if (string.IsNullOrWhiteSpace(r.TaxId) || string.IsNullOrWhiteSpace(r.TaxCountry)) { error = "taxId and taxCountry are required."; return false; }
        if (r.IdentityDocument is null || string.IsNullOrWhiteSpace(r.IdentityDocument.Type)
            || string.IsNullOrWhiteSpace(r.IdentityDocument.IssuingCountry)) { error = "a complete identityDocument is required."; return false; }
        if (!TryParseIsoDate(r.BirthDate, out birthDate)) { error = "birthDate must be an ISO-8601 date."; return false; }
        if (!TryParseIsoDate(r.IdentityDocument.ExpiryDate, out expiryDate)) { error = "identityDocument.expiryDate must be an ISO-8601 date."; return false; }
        return true;
    }

    private static bool TryParseIsoDate(string value, out DateTimeOffset date) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date);
}

/// <summary>Flow 1 — report where the caller's sign-up has got to, from Upvest.</summary>
public sealed class GetInvestorEndpoint : PortfolioEndpointBase, IEndpoint<IResult>
{
    public GetInvestorEndpoint(
        IUpvestPortfolioGateway gateway, IPortfolioStore store, IHttpContextAccessor http,
        ILogger<GetInvestorEndpoint> logger) : base(gateway, store, http, logger) { }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async () => await HandleAsync())
            .Produces<InvestorResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        var buyerId = BuyerId;
        if (buyerId is null)
        {
            return Results.Unauthorized();
        }

        var investor = Store.GetInvestor(buyerId);
        if (investor is null || investor.UpvestUserId is null)
        {
            return Results.NotFound(new { error = "You are not registered as an investor." });
        }

        try
        {
            // Scope-limited environments degrade to "pending"; a genuine provider failure surfaces as 502.
            var status = await PortfolioShared.ResolveInvestorStatusAsync(Gateway, investor.UpvestUserId.Value, investor.AccountId, Ct);
            return Results.Ok(new InvestorResponse
            {
                InvestorId = investor.UpvestUserId.Value.ToString(),
                Status = PortfolioShared.ToWire(status),
            });
        }
        catch (UpvestGatewayException ex)
        {
            return PortfolioShared.ToProblem(ex);
        }
    }
}
