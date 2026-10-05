using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Investing;

/// <summary>
/// Applies Upvest webhook events to investing state for immediacy. The reconciliation poller
/// is the source of truth, so this is best-effort and tolerant of unknown or malformed events.
/// </summary>
public sealed class UpvestWebhookProcessor
{
    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Investment> _investments;
    private readonly ILogger<UpvestWebhookProcessor> _logger;

    public UpvestWebhookProcessor(
        IRepository<Investor> investors,
        IRepository<Investment> investments,
        ILogger<UpvestWebhookProcessor> logger)
    {
        _investors = investors;
        _investments = investments;
        _logger = logger;
    }

    public async Task ProcessAsync(string rawJson, CancellationToken ct = default)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Discarded an unparseable Upvest webhook payload.");
            return;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("payload", out var events) || events.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var evt in events.EnumerateArray())
            {
                var type = evt.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (string.IsNullOrEmpty(type) || !evt.TryGetProperty("object", out var obj))
                {
                    continue;
                }

                try
                {
                    await HandleEventAsync(type, obj, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to apply Upvest webhook event {Type}.", type);
                }
            }
        }
    }

    private async Task HandleEventAsync(string type, JsonElement obj, CancellationToken ct)
    {
        switch (type)
        {
            case "USER.ACTIVATED":
                await ActivateInvestorAsync(GetString(obj, "id"), ct).ConfigureAwait(false);
                break;

            case "USER.REJECTED":
            case "USER_CHECK.FAILED":
                await RejectInvestorAsync(GetString(obj, "user_id") ?? GetString(obj, "id"), ct).ConfigureAwait(false);
                break;

            case "ORDER.FILLED":
            case "EXECUTION.SETTLED":
                await SettleInvestmentAsync(GetString(obj, "order_id") ?? GetString(obj, "id"), ct).ConfigureAwait(false);
                break;

            case "ORDER.CANCELLED":
                await FailInvestmentAsync(GetString(obj, "id"), ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task ActivateInvestorAsync(string? upvestUserId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(upvestUserId)) return;
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUpvestUserIdSpecification(upvestUserId), ct).ConfigureAwait(false);
        if (investor is not null && investor.Status == InvestorStatus.Pending)
        {
            investor.Activate();
            await _investors.UpdateAsync(investor, ct).ConfigureAwait(false);
            _logger.LogInformation("Enrolment {EnrolmentId} activated via webhook.", investor.EnrolmentId);
        }
    }

    private async Task RejectInvestorAsync(string? upvestUserId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(upvestUserId)) return;
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUpvestUserIdSpecification(upvestUserId), ct).ConfigureAwait(false);
        if (investor is not null && investor.Status == InvestorStatus.Pending)
        {
            investor.Reject();
            await _investors.UpdateAsync(investor, ct).ConfigureAwait(false);
            _logger.LogInformation("Enrolment {EnrolmentId} rejected via webhook.", investor.EnrolmentId);
        }
    }

    private async Task SettleInvestmentAsync(string? orderId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(orderId)) return;
        var investment = await _investments.FirstOrDefaultAsync(new InvestmentByUpvestOrderIdSpecification(orderId), ct).ConfigureAwait(false);
        if (investment is not null && investment.Status == InvestmentStatus.Pending)
        {
            investment.MarkSettled();
            await _investments.UpdateAsync(investment, ct).ConfigureAwait(false);
            _logger.LogInformation("Investment {InvestmentId} settled via webhook.", investment.InvestmentId);
        }
    }

    private async Task FailInvestmentAsync(string? orderId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(orderId)) return;
        var investment = await _investments.FirstOrDefaultAsync(new InvestmentByUpvestOrderIdSpecification(orderId), ct).ConfigureAwait(false);
        if (investment is not null && investment.Status == InvestmentStatus.Pending)
        {
            investment.MarkFailed();
            await _investments.UpdateAsync(investment, ct).ConfigureAwait(false);
            var investor = await _investors.GetByIdAsync(investment.InvestorId, ct).ConfigureAwait(false);
            if (investor is not null)
            {
                investor.ReturnFailedAmount(investment.Amount);
                await _investors.UpdateAsync(investor, ct).ConfigureAwait(false);
            }
            _logger.LogInformation("Investment {InvestmentId} failed via webhook.", investment.InvestmentId);
        }
    }

    private static string? GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
