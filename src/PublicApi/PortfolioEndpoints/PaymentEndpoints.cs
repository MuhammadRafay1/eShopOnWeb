using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>The signed-in investor pays an amount in (euros), invested across their model's funds.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class PayInEndpoint : EndpointBaseAsync
    .WithRequest<PayInRequest>
    .WithActionResult<PaymentResponse>
{
    private readonly IPortfolioService _portfolioService;

    public PayInEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpPost("api/portfolio/payments")]
    [SwaggerOperation(Summary = "Pay money into your portfolio", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<PaymentResponse>> HandleAsync([FromBody] PayInRequest request, CancellationToken cancellationToken = default)
    {
        var userName = User.Identity!.Name!;
        var payment = await _portfolioService.PayInAsync(userName, request.Amount, request.IdempotencyKey, cancellationToken);
        return Ok(ToResponse(payment));
    }

    internal static PaymentResponse ToResponse(PortfolioPayment payment) => new()
    {
        PaymentId = payment.PaymentId,
        Amount = decimal.Round(payment.Amount, 2),
        Status = PortfolioStatusText.From(payment.Status)
    };
}

/// <summary>Lists the caller's payments, newest first.</summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ListPaymentsEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IEnumerable<PaymentResponse>>
{
    private readonly IPortfolioService _portfolioService;

    public ListPaymentsEndpoint(IPortfolioService portfolioService) => _portfolioService = portfolioService;

    [HttpGet("api/portfolio/payments")]
    [SwaggerOperation(Summary = "List your payments", Tags = new[] { "PortfolioEndpoints" })]
    public override async Task<ActionResult<IEnumerable<PaymentResponse>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var userName = User.Identity!.Name!;
        var payments = await _portfolioService.GetPaymentsAsync(userName, cancellationToken);
        return Ok(payments.Select(PayInEndpoint.ToResponse).ToList());
    }
}
