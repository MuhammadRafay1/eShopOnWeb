using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// The callback Upvest itself calls. It is the only route that does not take a
/// shopper token. A webhook merely prompts an immediate reconciliation sweep;
/// the sweep independently fetches the truth from Upvest, so the notification
/// cannot inject state and needs no shopper identity.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, IInvestmentReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest-webhook",
            async (IInvestmentReconciliationService reconciliation) =>
            {
                return await HandleAsync(reconciliation);
            })
            .AllowAnonymous()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestmentReconciliationService reconciliation)
    {
        await reconciliation.ReconcileAsync(default);
        return Results.Ok();
    }
}
