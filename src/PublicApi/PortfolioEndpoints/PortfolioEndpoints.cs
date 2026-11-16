using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

using Roles = BlazorShared.Authorization.Constants.Roles;

// ───────────────────────────── Flow 1 — becoming an investor ─────────────────────────────

/// <summary>Signs the signed-in shopper up as an investor with Upvest.</summary>
public class RegisterInvestorEndpoint : IEndpoint<IResult, InvestorSignUpRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (InvestorSignUpRequest request, ClaimsPrincipal user, IManagedPortfolioService service) =>
            {
                request.ShopUserId = CurrentUser.Name(user);
                return await HandleAsync(request, service);
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(InvestorSignUpRequest request, IManagedPortfolioService service)
    {
        var signUp = new InvestorSignUp(
            request.FirstName, request.LastName, request.Email, request.BirthDate,
            request.BirthCity, request.BirthCountry, request.Nationality,
            new PostalAddress(request.Address.Line1, request.Address.Postcode, request.Address.City, request.Address.Country),
            request.PhoneNumber, request.TaxId, request.TaxCountry,
            new IdentityDocument(request.IdentityDocument.Type, request.IdentityDocument.Number, request.IdentityDocument.IssuingCountry, request.IdentityDocument.ExpiryDate));

        var view = await service.SignUpInvestorAsync(request.ShopUserId, signUp);
        return Results.Ok(PortfolioMapping.ToResponse(view));
    }
}

/// <summary>Where the caller's investor sign-up has got to.</summary>
public class GetInvestorEndpoint : IEndpoint<IResult, GetInvestorRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IManagedPortfolioService service) =>
                await HandleAsync(new GetInvestorRequest { ShopUserId = CurrentUser.Name(user) }, service))
            .Produces<InvestorResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(GetInvestorRequest request, IManagedPortfolioService service)
    {
        var view = await service.GetInvestorAsync(request.ShopUserId);
        return view is null ? Results.NotFound() : Results.Ok(PortfolioMapping.ToResponse(view));
    }
}

// ───────────────────────────── Flow 2 — model portfolios ─────────────────────────────

/// <summary>Operator action: create or replace a model portfolio.</summary>
public class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId}",
            [Authorize(Roles = Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, UpsertModelRequest request, IManagedPortfolioService service) =>
            {
                request.ModelId = modelId;
                return await HandleAsync(request, service);
            })
            .Produces<ModelResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(UpsertModelRequest request, IManagedPortfolioService service)
    {
        var funds = request.Funds.Select(f => new FundWeight(f.InstrumentId, f.Weight)).ToList();
        var view = await service.UpsertModelAsync(request.ModelId, request.Name, funds);
        return Results.Ok(PortfolioMapping.ToResponse(view));
    }
}

/// <summary>Every model portfolio and its funds.</summary>
public class ListModelsEndpoint : IEndpoint<IResult, GetModelsRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IManagedPortfolioService service) => await HandleAsync(new GetModelsRequest(), service))
            .Produces<ModelsResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(GetModelsRequest request, IManagedPortfolioService service)
    {
        var models = await service.ListModelsAsync();
        return Results.Ok(new ModelsResponse { Models = models.Select(PortfolioMapping.ToResponse).ToList() });
    }
}

/// <summary>The signed-in investor chooses which model their portfolio follows.</summary>
public class SelectModelEndpoint : IEndpoint<IResult, SelectModelRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SelectModelRequest request, ClaimsPrincipal user, IManagedPortfolioService service) =>
            {
                request.ShopUserId = CurrentUser.Name(user);
                return await HandleAsync(request, service);
            })
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(SelectModelRequest request, IManagedPortfolioService service)
    {
        await service.SelectModelAsync(request.ShopUserId, request.ModelId);
        return Results.NoContent();
    }
}

// ───────────────────────────── Flow 3 — paying in ─────────────────────────────

/// <summary>The signed-in investor pays an amount in.</summary>
public class CreatePaymentEndpoint : IEndpoint<IResult, CreatePaymentRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentRequest request, ClaimsPrincipal user, IManagedPortfolioService service) =>
            {
                request.ShopUserId = CurrentUser.Name(user);
                return await HandleAsync(request, service);
            })
            .Produces<PaymentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CreatePaymentRequest request, IManagedPortfolioService service)
    {
        var view = await service.PayInAsync(request.ShopUserId, request.Amount);
        return Results.Ok(PortfolioMapping.ToResponse(view));
    }
}

/// <summary>The caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint<IResult, GetPaymentsRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IManagedPortfolioService service) =>
                await HandleAsync(new GetPaymentsRequest { ShopUserId = CurrentUser.Name(user) }, service))
            .Produces<PaymentsResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(GetPaymentsRequest request, IManagedPortfolioService service)
    {
        var payments = await service.ListPaymentsAsync(request.ShopUserId);
        return Results.Ok(new PaymentsResponse { Payments = payments.Select(PortfolioMapping.ToResponse).ToList() });
    }
}

// ───────────────────────────── Flow 4 — realignment ─────────────────────────────

/// <summary>Operator action: realign every portfolio following a model.</summary>
public class TriggerRealignmentEndpoint : IEndpoint<IResult, TriggerRealignmentRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, IManagedPortfolioService service) =>
                await HandleAsync(new TriggerRealignmentRequest { ModelId = modelId }, service))
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(TriggerRealignmentRequest request, IManagedPortfolioService service)
    {
        var view = await service.TriggerRealignmentAsync(request.ModelId);
        return Results.Ok(PortfolioMapping.ToResponse(view));
    }
}

/// <summary>Where a realignment has got to.</summary>
public class GetRealignmentEndpoint : IEndpoint<IResult, GetRealignmentRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:guid}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (Guid realignmentId, IManagedPortfolioService service) =>
                await HandleAsync(new GetRealignmentRequest { RealignmentId = realignmentId }, service))
            .Produces<RealignmentResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(GetRealignmentRequest request, IManagedPortfolioService service)
    {
        var view = await service.GetRealignmentAsync(request.RealignmentId);
        return view is null ? Results.NotFound() : Results.Ok(PortfolioMapping.ToResponse(view));
    }
}

// ───────────────────────────── Flow 5 — the investor's portfolio ─────────────────────────────

/// <summary>The caller's portfolio as Upvest currently records it.</summary>
public class GetPortfolioEndpoint : IEndpoint<IResult, GetPortfolioRequest, IManagedPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IManagedPortfolioService service) =>
                await HandleAsync(new GetPortfolioRequest { ShopUserId = CurrentUser.Name(user) }, service))
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(GetPortfolioRequest request, IManagedPortfolioService service)
    {
        var view = await service.GetPortfolioAsync(request.ShopUserId);
        return Results.Ok(PortfolioMapping.ToResponse(view));
    }
}
