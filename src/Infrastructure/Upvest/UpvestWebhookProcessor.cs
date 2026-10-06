using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public interface IUpvestWebhookProcessor
{
    Task ProcessAsync(string eventType, JsonElement eventObject, CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies Upvest webhook events to local state. Idempotent: applying an event whose effect has
/// already happened (e.g. via the reconciler) is a no-op, so webhooks and polling can both run.
/// </summary>
public sealed class UpvestWebhookProcessor : IUpvestWebhookProcessor
{
    private readonly IRepository<Investor> _investors;
    private readonly IInvestorMutationGate _gate;
    private readonly ILogger<UpvestWebhookProcessor> _logger;

    public UpvestWebhookProcessor(IRepository<Investor> investors, IInvestorMutationGate gate, ILogger<UpvestWebhookProcessor> logger)
    {
        _investors = investors;
        _gate = gate;
        _logger = logger;
    }

    public async Task ProcessAsync(string eventType, JsonElement eventObject, CancellationToken cancellationToken = default)
    {
        switch (eventType)
        {
            case "USER_CHECK.PASSED":
            case "USER_CHECK.FAILED":
                await HandleCheckAsync(eventType, eventObject, cancellationToken).ConfigureAwait(false);
                break;
            case "ORDER.FILLED":
            case "ORDER.CANCELLED":
                await HandleOrderAsync(eventType, eventObject, cancellationToken).ConfigureAwait(false);
                break;
            default:
                // Intermediate states (USER_CHECK.CREATED, ORDER.NEW/PROCESSING) need no action.
                break;
        }
    }

    private Task HandleCheckAsync(string eventType, JsonElement obj, CancellationToken token)
    {
        if (!TryGetString(obj, "user_id", out var userId)) return Task.CompletedTask;

        // Serialised with the reconciler and order endpoint so concurrent writers don't lose updates.
        return _gate.RunAsync(async () =>
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByUpvestUserSpecification(userId), token).ConfigureAwait(false);
            if (investor is null || investor.Status != EnrolmentStatus.Pending) return;

            if (eventType == "USER_CHECK.PASSED") investor.Activate();
            else investor.Reject();

            await _investors.UpdateAsync(investor, token).ConfigureAwait(false);
            _logger.LogInformation("Webhook advanced enrolment {EnrolmentId} to {Status}.", investor.PublicId, investor.Status);
        }, token);
    }

    private Task HandleOrderAsync(string eventType, JsonElement obj, CancellationToken token)
    {
        if (!TryGetString(obj, "id", out var orderId)) return Task.CompletedTask;

        return _gate.RunAsync(async () =>
        {
            var all = await _investors.ListAsync(new InvestorsWithUnsettledInvestmentsSpecification(), token).ConfigureAwait(false);
            foreach (var investor in all)
            {
                var investment = investor.Investments.FirstOrDefault(i => i.UpvestOrderId == orderId && i.Status == InvestmentStatus.Pending);
                if (investment is null) continue;

                var status = eventType == "ORDER.FILLED" ? InvestmentStatus.Settled : InvestmentStatus.Failed;
                investor.SettleInvestment(investment, status);
                await _investors.UpdateAsync(investor, token).ConfigureAwait(false);
                _logger.LogInformation("Webhook settled investment {InvestmentId} to {Status}.", investment.PublicId, investment.Status);
                return;
            }
        }, token);
    }

    private static bool TryGetString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString() ?? string.Empty;
            return !string.IsNullOrEmpty(value);
        }
        return false;
    }
}
