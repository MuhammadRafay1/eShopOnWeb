using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolios;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Maps the managed investment portfolio HTTP endpoints under <c>/api/portfolio</c>. Every endpoint is
/// JWT-authenticated; the caller's identity comes from the token. Operator-only endpoints additionally
/// require the Administrators role. Each capability is a separately invocable route.
/// </summary>
public static class PortfolioEndpoints
{
    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var userAuth = new AuthorizeAttribute { AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme };
        var adminAuth = new AuthorizeAttribute
        {
            Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme
        };
        const string Tag = "Portfolio";

        // ---------------------------------------------------------- Flow 1: becoming an investor
        app.MapPost("/api/portfolio/investor",
            async (InvestorSignUpRequest request, ClaimsPrincipal user, IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    var shopper = Shopper(user);
                    var form = ToSignUp(request);
                    var investor = await svc.SignUpInvestorAsync(shopper, form, ct);
                    return Results.Created("/api/portfolio/investor", ToInvestorResponse(investor));
                }))
            .RequireAuthorization(userAuth).WithTags(Tag);

        app.MapGet("/api/portfolio/investor",
            async (ClaimsPrincipal user, IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    var investor = await svc.GetInvestorAsync(Shopper(user), ct);
                    return investor is null
                        ? Results.NotFound(new { message = "You are not registered as an investor." })
                        : Results.Ok(ToInvestorResponse(investor));
                }))
            .RequireAuthorization(userAuth).WithTags(Tag);

        // ---------------------------------------------------------- Flow 2: model portfolios
        app.MapPut("/api/portfolio/models/{modelId}",
            async (string modelId, ModelPutRequest request, IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    var funds = (request.Funds ?? new List<FundInputDto>())
                        .Select(f => new ModelFundInput(f.InstrumentId, f.Weight)).ToList();
                    var model = await svc.UpsertModelAsync(modelId, request.Name, funds, ct);
                    return Results.Ok(ToModelResponse(model));
                }))
            .RequireAuthorization(adminAuth).WithTags(Tag);

        app.MapGet("/api/portfolio/models",
            async (IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    var models = await svc.ListModelsAsync(ct);
                    return Results.Ok(models.Select(ToModelResponse).ToList());
                }))
            .RequireAuthorization(userAuth).WithTags(Tag);

        app.MapPut("/api/portfolio/investor/model",
            async (ChooseModelRequest request, ClaimsPrincipal user, IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    var investor = await svc.ChooseModelAsync(Shopper(user), request.ModelId, ct);
                    return Results.Ok(ToInvestorResponse(investor));
                }))
            .RequireAuthorization(userAuth).WithTags(Tag);

        // ---------------------------------------------------------- Flow 3: paying in
        app.MapPost("/api/portfolio/payments",
            async (PayInRequest request, ClaimsPrincipal user, IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    var payment = await svc.PayInAsync(Shopper(user), request.Amount, ct);
                    return Results.Created("/api/portfolio/payments",
                        new PaymentCreatedResponse { PaymentId = payment.Id.ToString(), Status = PaymentStatusText(payment.Status) });
                }))
            .RequireAuthorization(userAuth).WithTags(Tag);

        app.MapGet("/api/portfolio/payments",
            async (ClaimsPrincipal user, IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    var payments = await svc.ListPaymentsAsync(Shopper(user), ct);
                    return Results.Ok(payments.Select(ToPaymentResponse).ToList());
                }))
            .RequireAuthorization(userAuth).WithTags(Tag);

        // ---------------------------------------------------------- Flow 4: realignment
        app.MapPost("/api/portfolio/models/{modelId}/realignments",
            async (string modelId, IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    var realignment = await svc.CreateRealignmentAsync(modelId, ct);
                    return Results.Created($"/api/portfolio/realignments/{realignment.Id}", ToRealignmentResponse(realignment));
                }))
            .RequireAuthorization(adminAuth).WithTags(Tag);

        app.MapGet("/api/portfolio/realignments/{realignmentId}",
            async (string realignmentId, IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    if (!Guid.TryParse(realignmentId, out var id))
                        return Results.BadRequest(new { message = "Invalid realignment id." });
                    var realignment = await svc.GetRealignmentAsync(id, ct);
                    return realignment is null
                        ? Results.NotFound(new { message = "No such realignment." })
                        : Results.Ok(ToRealignmentResponse(realignment));
                }))
            .RequireAuthorization(userAuth).WithTags(Tag);

        // ---------------------------------------------------------- Flow 5: the investor's portfolio
        app.MapGet("/api/portfolio",
            async (ClaimsPrincipal user, IPortfolioService svc, CancellationToken ct) =>
                await Guarded(async () =>
                {
                    var view = await svc.GetPortfolioAsync(Shopper(user), ct);
                    return view is null
                        ? Results.NotFound(new { message = "You are not registered as an investor." })
                        : Results.Ok(ToPortfolioResponse(view));
                }))
            .RequireAuthorization(userAuth).WithTags(Tag);

        return app;
    }

    // ------------------------------------------------------------------ guards & identity

    private static string Shopper(ClaimsPrincipal user)
    {
        var name = user.Identity?.Name
                   ?? user.FindFirstValue(ClaimTypes.Name)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(name))
            throw new PortfolioValidationException("The authenticated caller could not be identified.");
        return name;
    }

    /// <summary>Translates domain errors to HTTP results; everything else bubbles to the exception middleware.</summary>
    private static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (PortfolioValidationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (NotAnInvestorException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (PortfolioConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (UpvestApiException ex)
        {
            // Upvest refused or could not complete the call; surface it honestly as an upstream error.
            return Results.Json(new { message = ex.Message, upvestStatus = ex.StatusCode },
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    // ------------------------------------------------------------------ mapping

    private static InvestorSignUp ToSignUp(InvestorSignUpRequest r) => new(
        r.FirstName, r.LastName, r.Email, r.BirthDate, r.BirthCity, r.BirthCountry, r.Nationality,
        new UpvestAddress(r.Address.Line1, r.Address.Postcode, r.Address.City, r.Address.Country),
        r.PhoneNumber, r.TaxId, r.TaxCountry,
        new UpvestIdentityDocument(r.IdentityDocument.Type, r.IdentityDocument.Number, r.IdentityDocument.IssuingCountry, r.IdentityDocument.ExpiryDate));

    private static InvestorResponse ToInvestorResponse(Investor investor) => new()
    {
        InvestorId = investor.Id.ToString(),
        Status = InvestorStatusText(investor.Status),
        Detail = investor.StatusDetail
    };

    private static ModelResponse ToModelResponse(ModelPortfolio model) => new()
    {
        ModelId = model.ModelId,
        Name = model.Name,
        Funds = model.Funds.Select(f => new FundDto { InstrumentId = f.InstrumentId, Weight = f.Weight }).ToList()
    };

    private static PaymentResponse ToPaymentResponse(Payment payment) => new()
    {
        PaymentId = payment.Id.ToString(),
        Amount = payment.Amount,
        Status = PaymentStatusText(payment.Status)
    };

    private static RealignmentResponse ToRealignmentResponse(Realignment realignment) => new()
    {
        RealignmentId = realignment.Id.ToString(),
        Status = RealignmentStatusText(realignment.Status),
        Detail = realignment.StatusDetail
    };

    private static PortfolioResponse ToPortfolioResponse(PortfolioView view) => new()
    {
        ModelId = view.ModelId,
        Holdings = view.Holdings.Select(h => new HoldingDto
        {
            InstrumentId = h.InstrumentId,
            Quantity = h.Quantity,
            Value = h.Value
        }).ToList(),
        TotalValue = view.TotalValue,
        PaidIn = view.PaidIn
    };

    private static string InvestorStatusText(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending"
    };

    private static string PaymentStatusText(PaymentStatus status) => status switch
    {
        PaymentStatus.Invested => "invested",
        PaymentStatus.Failed => "failed",
        _ => "pending"
    };

    private static string RealignmentStatusText(RealignmentStatus status) => status switch
    {
        RealignmentStatus.Completed => "completed",
        RealignmentStatus.Failed => "failed",
        _ => "pending"
    };
}
