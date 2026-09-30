using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SaveCardRequest
{
    public string Number { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string SecurityCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SaveCardBillingAddressRequest BillingAddress { get; set; } = new();
}

public class SaveCardBillingAddressRequest
{
    public string CountryCode { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AdminArea1 { get; set; }
    public string? AdminArea2 { get; set; }
    public string? PostalCode { get; set; }
}

public class CreatePaymentMethodRequest
{
    public string BuyerId { get; set; } = string.Empty;
    public SaveCardRequest Card { get; set; } = new();
    public string? Alias { get; set; }
}

public class CreatePaymentMethodResponse
{
    public int PaymentMethodId { get; set; }
    public string? Brand { get; set; }
    public string? Last4 { get; set; }
    public string? Expiry { get; set; }
    public string? Alias { get; set; }
}

/// <summary>
/// Vaults a card with PayPal and stores only a safe descriptor (brand/last4/expiry) plus the
/// vault token id for the signed-in shopper. Full card details are never stored by this app.
/// </summary>
public class CreatePaymentMethodEndpoint : IEndpoint<IResult, CreatePaymentMethodRequest, PaymentMethodEndpointServices>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/payment-methods",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentMethodRequest request, ClaimsPrincipal user, PaymentMethodEndpointServices services) =>
            {
                request.BuyerId = user.Identity!.Name!;
                return await HandleAsync(request, services);
            })
            .Produces<CreatePaymentMethodResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status502BadGateway)
            .WithTags("PaymentMethodEndpoints");
    }

    public async Task<IResult> HandleAsync(CreatePaymentMethodRequest request, PaymentMethodEndpointServices services)
    {
        var buyer = await services.BuyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(request.BuyerId));
        var isNewBuyer = buyer is null;
        buyer ??= new Buyer(request.BuyerId);

        var card = new CardDetails(
            request.Card.Number,
            request.Card.ExpiryMonth,
            request.Card.ExpiryYear,
            request.Card.SecurityCode,
            request.Card.Name,
            new BillingAddress(
                request.Card.BillingAddress.CountryCode,
                request.Card.BillingAddress.AddressLine1,
                request.Card.BillingAddress.AdminArea1,
                request.Card.BillingAddress.AdminArea2,
                request.Card.BillingAddress.PostalCode));

        PayPalVaultResult vaulted;
        try
        {
            vaulted = await services.PaymentGateway.SaveCardAsync(card, buyer.PayPalCustomerId);
        }
        catch (PayPalActionRequiredException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
        catch (PayPalGatewayException ex)
        {
            return Results.Json(new { message = ex.Message, debugId = ex.DebugId }, statusCode: StatusCodes.Status502BadGateway);
        }

        if (vaulted.CustomerId is not null)
            buyer.SetPayPalCustomerId(vaulted.CustomerId);

        var paymentMethod = buyer.AddPaymentMethod(vaulted.VaultTokenId, vaulted.Last4, vaulted.Brand, vaulted.Expiry, request.Alias);

        if (isNewBuyer)
            await services.BuyerRepository.AddAsync(buyer);
        else
            await services.BuyerRepository.UpdateAsync(buyer);

        return Results.Created($"api/payment-methods/{paymentMethod.Id}", new CreatePaymentMethodResponse
        {
            PaymentMethodId = paymentMethod.Id,
            Brand = paymentMethod.Brand,
            Last4 = paymentMethod.Last4,
            Expiry = paymentMethod.Expiry,
            Alias = paymentMethod.Alias
        });
    }
}
