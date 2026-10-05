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
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    // The shop has no shipping step for API-placed orders; a neutral placeholder satisfies the
    // existing Order model's required address without inventing a parallel order type.
    private static readonly Address PlaceholderShipToAddress =
        new("N/A", "N/A", "N/A", "N/A", "00000");

    private readonly IRepository<Investor> _investorRepository;
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUpvestClient _upvestClient;
    private readonly IUriComposer _uriComposer;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investorRepository,
        IRepository<Order> orderRepository,
        IRepository<CatalogItem> itemRepository,
        IUpvestClient upvestClient,
        IUriComposer uriComposer,
        IAppLogger<InvestingService> logger)
    {
        _investorRepository = investorRepository;
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _upvestClient = upvestClient;
        _uriComposer = uriComposer;
        _logger = logger;
    }

    public async Task<EnrolmentResult> EnrolAsync(string buyerId, InvestorSignUp form, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(form, nameof(form));

        var existing = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (existing is not null)
        {
            // Enrolment is idempotent per shopper; return where they already stand.
            return new EnrolmentResult(existing.EnrolmentId, existing.Status);
        }

        // Submit the shopper to Upvest as an investor. These calls carry the personal details,
        // which are never persisted or logged by this application.
        var upvestUser = await _upvestClient.CreateUserAsync(form, cancellationToken);
        await _upvestClient.CreateTaxResidencyAsync(upvestUser.Id, form.TaxCountry, form.TaxId, cancellationToken);
        await _upvestClient.CreateKycCheckAsync(upvestUser.Id, form, cancellationToken);
        await _upvestClient.CreateProofOfResidencyCheckAsync(upvestUser.Id, form, cancellationToken);

        var investor = new Investor(buyerId);
        investor.SetUpvestUserId(upvestUser.Id);
        await _investorRepository.AddAsync(investor, cancellationToken);

        _logger.LogInformation($"Enrolled shopper into investing; enrolment {investor.EnrolmentId} is {investor.Status}.");
        return new EnrolmentResult(investor.EnrolmentId, investor.Status);
    }

    public async Task<EnrolmentResult?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        return investor is null ? null : new EnrolmentResult(investor.EnrolmentId, investor.Status);
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(lines, nameof(lines));
        if (lines.Count == 0)
            throw new ArgumentException("An order must contain at least one item.", nameof(lines));

        var catalogItemIds = lines.Select(l => l.CatalogItemId).Distinct().ToArray();
        var catalogItems = await _itemRepository.ListAsync(new CatalogItemsSpecification(catalogItemIds), cancellationToken);

        var orderItems = new List<OrderItem>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException($"Quantity for catalog item {line.CatalogItemId} must be positive.", nameof(lines));

            var catalogItem = catalogItems.FirstOrDefault(c => c.Id == line.CatalogItemId)
                ?? throw new ArgumentException($"Catalog item {line.CatalogItemId} does not exist.", nameof(lines));

            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            orderItems.Add(new OrderItem(itemOrdered, catalogItem.Price, line.Quantity));
        }

        var order = new Order(buyerId, PlaceholderShipToAddress, orderItems);
        await _orderRepository.AddAsync(order, cancellationToken);

        // The order is now placed and paid. Setting aside the change must never fail the order.
        long roundUpCents = 0;
        try
        {
            roundUpCents = await SetAsideChangeAsync(buyerId, order.Total(), cancellationToken);
        }
        catch (Exception ex)
        {
            // Swallow: the order stands and the caller's request still succeeds.
            _logger.LogWarning($"Setting aside change failed for order {order.Id}; the order was still placed. {ex.GetType().Name}");
            roundUpCents = 0;
        }

        return new PlaceOrderResult(order.Id, roundUpCents);
    }

    private async Task<long> SetAsideChangeAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken)
    {
        var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null || !investor.IsAccepted)
            return 0;

        var totalCents = ToCents(orderTotal);
        var roundUp = investor.SetAsideRoundUp(totalCents);

        // When the balance reaches the threshold, begin a (local) pending investment now; the
        // background reconciler places and settles it at Upvest.
        investor.TryBeginInvestment();

        await _investorRepository.UpdateAsync(investor, cancellationToken);
        return roundUp;
    }

    public async Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
            return Array.Empty<InvestmentView>();

        return investor.Investments
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvestmentView(i.PublicId, i.AmountCents, i.Status))
            .ToList();
    }

    public async Task<BalanceResult?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        return investor is null ? null : new BalanceResult(investor.SetAsideCents, investor.InvestedCents);
    }

    public async Task<bool> SettleInvestmentByUpvestOrderAsync(string upvestOrderId, string upvestOrderStatus, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(upvestOrderId)) return false;

        var investor = await _investorRepository.FirstOrDefaultAsync(new InvestorByUpvestOrderIdSpecification(upvestOrderId), cancellationToken);
        var investment = investor?.Investments.FirstOrDefault(i => i.UpvestOrderId == upvestOrderId);
        if (investor is null || investment is null || investment.Status != InvestmentStatus.Pending)
            return false;

        if (string.Equals(upvestOrderStatus, UpvestStatuses.OrderFilled, StringComparison.OrdinalIgnoreCase))
        {
            investment.MarkSettled();
        }
        else if (string.Equals(upvestOrderStatus, UpvestStatuses.OrderCancelled, StringComparison.OrdinalIgnoreCase))
        {
            investment.MarkFailed();
            investor.RefundFailedInvestment(investment);
        }
        else
        {
            return false;
        }

        await _investorRepository.UpdateAsync(investor, cancellationToken);
        return true;
    }

    private static long ToCents(decimal euros) => (long)decimal.Round(euros * 100m, MidpointRounding.AwayFromZero);
}
