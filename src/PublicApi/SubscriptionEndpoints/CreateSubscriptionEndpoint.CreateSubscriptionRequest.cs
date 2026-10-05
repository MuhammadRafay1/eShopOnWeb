using System.Text.Json.Serialization;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class CreateSubscriptionRequest : BaseRequest
{
    /// <summary>Handle of the plan to subscribe to, as listed by GET /api/subscription-plans.</summary>
    public string? PlanHandle { get; set; }

    /// <summary>Optional billing first name; derived from the account e-mail when omitted.</summary>
    public string? FirstName { get; set; }

    /// <summary>Optional billing last name; derived from the account e-mail when omitted.</summary>
    public string? LastName { get; set; }

    /// <summary>The caller, resolved from the JWT — never bound from the request body.</summary>
    [JsonIgnore]
    public Subscriber? Subscriber { get; set; }
}
