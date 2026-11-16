using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Signs the signed-in shopper up as a managed-portfolio investor at Upvest.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RegisterInvestorEndpoint : EndpointBaseAsync
    .WithRequest<RegisterInvestorRequest>
    .WithActionResult<InvestorResponse>
{
    private readonly IPortfolioService _portfolioService;

    public RegisterInvestorEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpPost("api/portfolio/investor")]
    [SwaggerOperation(Summary = "Become an investor", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<InvestorResponse>> HandleAsync([FromBody] RegisterInvestorRequest request, CancellationToken cancellationToken = default)
    {
        var userName = User.Identity!.Name!;
        var registration = new InvestorRegistration(
            request.FirstName,
            request.LastName,
            request.Email,
            request.BirthDate,
            request.BirthCity,
            request.BirthCountry,
            request.Nationality,
            new RegistrationAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber,
            request.TaxId,
            request.TaxCountry,
            new IdentityDocument(request.IdentityDocument.Type, request.IdentityDocument.Number, request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

        var investor = await _portfolioService.RegisterInvestorAsync(userName, registration, cancellationToken);
        return Ok(ToResponse(investor));
    }

    internal static InvestorResponse ToResponse(Investor investor) => new()
    {
        InvestorId = investor.UpvestUserId,
        Status = PortfolioStatusText.From(investor.Status),
        ModelId = investor.ChosenModelId
    };
}

/// <summary>Reports where the signed-in shopper's investor sign-up has got to at Upvest.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GetInvestorEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<InvestorResponse>
{
    private readonly IPortfolioService _portfolioService;

    public GetInvestorEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpGet("api/portfolio/investor")]
    [SwaggerOperation(Summary = "Investor sign-up status", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<InvestorResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var userName = User.Identity!.Name!;
        var investor = await _portfolioService.GetInvestorAsync(userName, cancellationToken);
        if (investor is null)
        {
            return NotFound();
        }

        return Ok(RegisterInvestorEndpoint.ToResponse(investor));
    }
}
