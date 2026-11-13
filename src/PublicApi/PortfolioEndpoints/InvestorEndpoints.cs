using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 1 — sign the signed-in shopper up as an investor with Upvest.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CreateInvestorEndpoint : EndpointBaseAsync
    .WithRequest<InvestorSignupApiRequest>
    .WithActionResult<InvestorApiResponse>
{
    private readonly IInvestorService _service;

    public CreateInvestorEndpoint(IInvestorService service) => _service = service;

    [HttpPost("api/portfolio/investor")]
    [SwaggerOperation(Summary = "Sign up as an investor", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult<InvestorApiResponse>> HandleAsync(
        [FromBody] InvestorSignupApiRequest request, CancellationToken cancellationToken = default)
    {
        var shopperId = PortfolioApi.ShopperId(User);
        if (shopperId is null) return Unauthorized();

        if (request?.Address is null || request.IdentityDocument is null)
            return BadRequest("firstName, lastName, email, birthDate, address and identityDocument are required.");

        var details = new InvestorSignupDetails(
            request.FirstName, request.LastName, request.Email, request.BirthDate,
            request.BirthCity, request.BirthCountry, request.Nationality,
            new AddressDetails(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber, request.TaxId, request.TaxCountry,
            new IdentityDocumentDetails(request.IdentityDocument.Type, request.IdentityDocument.Number,
                request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

        try
        {
            var result = await _service.SignUpAsync(shopperId, details, cancellationToken);
            return Ok(PortfolioApi.ToResponse(result));
        }
        catch (PortfolioException ex)
        {
            return PortfolioApi.ToErrorResult(ex);
        }
    }
}

/// <summary>Flow 1 — the caller's current sign-up status.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GetInvestorEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<InvestorApiResponse>
{
    private readonly IInvestorService _service;

    public GetInvestorEndpoint(IInvestorService service) => _service = service;

    [HttpGet("api/portfolio/investor")]
    [SwaggerOperation(Summary = "Get the caller's investor status", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult<InvestorApiResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var shopperId = PortfolioApi.ShopperId(User);
        if (shopperId is null) return Unauthorized();

        try
        {
            var result = await _service.GetStatusAsync(shopperId, cancellationToken);
            return result is null ? NotFound() : Ok(PortfolioApi.ToResponse(result));
        }
        catch (PortfolioException ex)
        {
            return PortfolioApi.ToErrorResult(ex);
        }
    }
}
