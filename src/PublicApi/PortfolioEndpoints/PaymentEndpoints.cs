using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Swashbuckle.AspNetCore.Annotations;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 3 — the signed-in investor pays an amount in.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CreatePaymentEndpoint : EndpointBaseAsync
    .WithRequest<PayInApiRequest>
    .WithActionResult<PaymentApiResponse>
{
    private readonly IPaymentService _service;

    public CreatePaymentEndpoint(IPaymentService service) => _service = service;

    [HttpPost("api/portfolio/payments")]
    [SwaggerOperation(Summary = "Pay money into the caller's portfolio", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult<PaymentApiResponse>> HandleAsync(
        [FromBody] PayInApiRequest request, CancellationToken cancellationToken = default)
    {
        var shopperId = PortfolioApi.ShopperId(User);
        if (shopperId is null) return Unauthorized();
        if (request is null) return BadRequest("amount is required.");

        try
        {
            var result = await _service.PayInAsync(shopperId, request.Amount, cancellationToken);
            return Ok(PortfolioApi.ToResponse(result));
        }
        catch (PortfolioException ex)
        {
            return PortfolioApi.ToErrorResult(ex);
        }
    }
}

/// <summary>Flow 3 — the caller's payments, newest first.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ListPaymentsEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<List<PaymentApiResponse>>
{
    private readonly IPaymentService _service;

    public ListPaymentsEndpoint(IPaymentService service) => _service = service;

    [HttpGet("api/portfolio/payments")]
    [SwaggerOperation(Summary = "List the caller's payments", Tags = new[] { "Portfolio" })]
    public override async Task<ActionResult<List<PaymentApiResponse>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var shopperId = PortfolioApi.ShopperId(User);
        if (shopperId is null) return Unauthorized();

        try
        {
            var payments = await _service.ListPaymentsAsync(shopperId, cancellationToken);
            return Ok(payments.Select(PortfolioApi.ToResponse).ToList());
        }
        catch (PortfolioException ex)
        {
            return PortfolioApi.ToErrorResult(ex);
        }
    }
}
