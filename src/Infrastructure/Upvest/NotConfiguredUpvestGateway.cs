using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Stands in for <see cref="IUpvestGateway"/> when no Upvest configuration is present (e.g. in tests). The
/// rest of the API runs normally; any attempt to use the investing capability fails with a clear message.
/// </summary>
public sealed class NotConfiguredUpvestGateway : IUpvestGateway
{
    private static InvalidOperationException NotConfigured() =>
        new("The 'invest your change' capability is not configured (missing Upvest configuration).");

    public Task<string> RegisterInvestorAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task<UpvestEnrolment> TryCompleteEnrolmentAsync(string upvestUserId, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task<UpvestOrderResult> PlaceInvestmentOrderAsync(string upvestUserId, string upvestAccountGroupId, string upvestAccountId, decimal amountEuros, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task<InvestmentStatus> GetOrderStatusAsync(string upvestOrderId, CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task EnsureOrderWebhookAsync(CancellationToken cancellationToken = default) => throw NotConfigured();
}
