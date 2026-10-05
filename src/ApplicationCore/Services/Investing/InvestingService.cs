using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Investing;

/// <summary>
/// Request-path investing logic: enrolment, placing shop orders with round-up set-aside, and read models.
/// The multi-step provider orchestration (activation polling, account provisioning, investing, settlement)
/// lives in <see cref="InvestingProcessor"/>; this service keeps request handling fast and never lets
/// anything investing-related fail an order.
/// </summary>
public class InvestingService : IInvestingService
{
    private readonly IRepository<Enrolment> _enrolments;
    private readonly IRepository<Investment> _investments;
    private readonly IRepository<Order> _orders;
    private readonly IReadRepository<CatalogItem> _catalogItems;
    private readonly IUriComposer _uriComposer;
    private readonly IUpvestInvestorGateway _gateway;
    private readonly IShopperConcurrencyGuard _guard;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Enrolment> enrolments,
        IRepository<Investment> investments,
        IRepository<Order> orders,
        IReadRepository<CatalogItem> catalogItems,
        IUriComposer uriComposer,
        IUpvestInvestorGateway gateway,
        IShopperConcurrencyGuard guard,
        IAppLogger<InvestingService> logger)
    {
        _enrolments = enrolments;
        _investments = investments;
        _orders = orders;
        _catalogItems = catalogItems;
        _uriComposer = uriComposer;
        _gateway = gateway;
        _guard = guard;
        _logger = logger;
    }

    public async Task<Enrolment> EnrolAsync(string shopperId, InvestorSignUpDetails details, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.Null(details, nameof(details));

        using var _ = await _guard.LockAsync(shopperId, cancellationToken);

        var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(shopperId), cancellationToken);
        var isNew = enrolment is null;
        if (enrolment is null)
        {
            enrolment = new Enrolment(shopperId, Guid.NewGuid());
            enrolment = await _enrolments.AddAsync(enrolment, cancellationToken);
        }
        else if (enrolment.Status != EnrolmentStatus.Pending)
        {
            // Already accepted or rejected — enrolment is idempotent; return as-is.
            return enrolment;
        }

        // Drive whatever onboarding steps are still outstanding, using the freshly submitted form.
        try
        {
            if (enrolment.UpvestUserId is null)
            {
                var created = await _gateway.CreateInvestorAsync(details, enrolment.CreateUserIdempotencyKey, cancellationToken);
                enrolment.SetProviderInvestor(created.UpvestUserId);
                ApplyProviderStatus(enrolment, created.Status);
            }

            if (enrolment.UpvestUserId is Guid userId && !enrolment.OnboardingSubmitted && enrolment.Status != EnrolmentStatus.Rejected)
            {
                await _gateway.SubmitOnboardingEvidenceAsync(userId, details, enrolment.TaxIdempotencyKey, cancellationToken);
                enrolment.MarkOnboardingSubmitted();
            }
        }
        catch (Exception ex)
        {
            // Enrolment must not fail the request: the claim is persisted and the background processor
            // will carry the outstanding steps forward. Never log personal detail.
            _logger.LogWarning($"Enrolment onboarding for a shopper did not complete on the request path and will be retried in the background: {ex.Message}");
        }

        await _enrolments.UpdateAsync(enrolment, cancellationToken);
        if (isNew)
            _logger.LogInformation("Shopper enrolment created (enrolmentId {0}).", enrolment.Id);
        return enrolment;
    }

    private static void ApplyProviderStatus(Enrolment enrolment, ProviderInvestorStatus status)
    {
        switch (status)
        {
            case ProviderInvestorStatus.Active: enrolment.MarkActive(); break;
            case ProviderInvestorStatus.Rejected: enrolment.MarkRejected(); break;
        }
    }

    public Task<Enrolment?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken) =>
        _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(shopperId), cancellationToken);

    public async Task<PlaceOrderResult> PlaceOrderAsync(string shopperId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        if (lines is null || lines.Count == 0)
            throw new ArgumentException("An order must contain at least one item.", nameof(lines));
        foreach (var line in lines)
        {
            if (line.CatalogItemId <= 0) throw new ArgumentException("Invalid catalog item id.", nameof(lines));
            if (line.Quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(lines));
        }

        var ids = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _catalogItems.ListAsync(new CatalogItemsSpecification(ids), cancellationToken);
        var byId = catalogItems.ToDictionary(c => c.Id);

        var orderItems = new List<OrderItem>();
        foreach (var line in lines)
        {
            if (!byId.TryGetValue(line.CatalogItemId, out var catalogItem))
                throw new ArgumentException($"Catalog item {line.CatalogItemId} does not exist.", nameof(lines));

            var pictureUri = _uriComposer.ComposePicUri(string.IsNullOrEmpty(catalogItem.PictureUri) ? "eCatalog-item-default.png" : catalogItem.PictureUri);
            if (string.IsNullOrEmpty(pictureUri)) pictureUri = "eCatalog-item-default.png";
            var ordered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, pictureUri);
            orderItems.Add(new OrderItem(ordered, catalogItem.Price, line.Quantity));
        }

        // eShopOnWeb has no separate shipping step for this API; use a placeholder address.
        var shipToAddress = new Address("N/A", "N/A", "N/A", "N/A", "00000");
        var order = new Order(shopperId, shipToAddress, orderItems);
        order = await _orders.AddAsync(order, cancellationToken);

        // Order is placed. Everything below is best-effort and must never fail the request.
        var total = order.Total();
        var roundUp = decimal.Round(Math.Ceiling(total) - total, 2, MidpointRounding.AwayFromZero);
        var setAside = 0m;
        if (roundUp > 0m)
        {
            try
            {
                using var _ = await _guard.LockAsync(shopperId, cancellationToken);
                var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(shopperId), cancellationToken);
                if (enrolment is { Status: EnrolmentStatus.Active })
                {
                    enrolment.AddSetAside(roundUp);
                    await _enrolments.UpdateAsync(enrolment, cancellationToken);
                    setAside = roundUp;
                    _logger.LogInformation("Order {0} set aside {1} for investing.", order.Id, setAside);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Order {order.Id} placed but set-aside did not apply: {ex.Message}");
                setAside = 0m;
            }
        }

        return new PlaceOrderResult(order.Id, setAside);
    }

    public async Task<BalanceView> GetBalanceAsync(string shopperId, CancellationToken cancellationToken)
    {
        var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(shopperId), cancellationToken);
        var pending = enrolment?.PendingAmount ?? 0m;

        var investments = await _investments.ListAsync(new InvestmentsByShopperSpecification(shopperId), cancellationToken);
        // "Invested so far" = everything placed with the provider and not returned as failed.
        var invested = investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.Amount);

        return new BalanceView(decimal.Round(pending, 2, MidpointRounding.AwayFromZero), decimal.Round(invested, 2, MidpointRounding.AwayFromZero));
    }

    public async Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string shopperId, CancellationToken cancellationToken)
    {
        var investments = await _investments.ListAsync(new InvestmentsByShopperSpecification(shopperId), cancellationToken);
        return investments;
    }
}
