using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PortfolioPaymentService : IPortfolioPaymentService
{
    private const string Currency = "EUR";

    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IInvestorService _investorService;
    private readonly IUpvestClient _upvest;
    private readonly IAppLogger<PortfolioPaymentService> _logger;

    public PortfolioPaymentService(
        IRepository<PortfolioPayment> payments,
        IInvestorService investorService,
        IUpvestClient upvest,
        IAppLogger<PortfolioPaymentService> logger)
    {
        _payments = payments;
        _investorService = investorService;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<PortfolioPayment> PayInAsync(string shopUserName, decimal amount, CancellationToken ct = default)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        // Eligibility: must be an accepted (active) investor with a model chosen and an account ready.
        var investor = await _investorService.GetAsync(shopUserName, ct)
            ?? throw new PaymentRefusedException("You must sign up as an investor before paying in.");

        if (investor.MapStatus() != InvestorStatus.Active)
            throw new PaymentRefusedException("Your investor account is not active yet; you cannot pay in until Upvest has accepted you and your account is ready.");

        if (string.IsNullOrEmpty(investor.ModelId))
            throw new PaymentRefusedException("You must choose a model portfolio before paying in.");

        if (investor.AccountGroupId is null || investor.AccountId is null || investor.UpvestUserId is null)
            throw new PaymentRefusedException("Your investor account is not fully provisioned yet.");

        var payment = new PortfolioPayment(shopUserName, amount, Currency);

        // 1. Credit the cash to the investor's account at Upvest (sandbox funding via top-up).
        var topup = await _upvest.CreateTopupAsync(investor.AccountGroupId, amount, Currency, ct);
        payment.SetTopup(topup.Id, topup.Status);

        // Persist now so a credited-but-not-yet-invested state is never lost.
        await _payments.AddAsync(payment, ct);

        // 2. Invest the cash across the funds of the model the investor follows (allocation-weighted order).
        var order = await _upvest.CreatePortfolioOrderAsync(investor.UpvestUserId, investor.AccountId, amount, Currency, ct);
        payment.SetOrder(order.Id, order.Status);
        await _payments.UpdateAsync(payment, ct);

        return payment;
    }

    public async Task<IReadOnlyList<PortfolioPayment>> ListAsync(string shopUserName, CancellationToken ct = default)
    {
        var payments = await _payments.ListAsync(new PaymentsByShopUserSpecification(shopUserName), ct);

        // Refresh each payment's outcome from Upvest (no webhooks — we poll).
        foreach (var payment in payments)
        {
            if (payment.UpvestOrderId is null) continue;
            try
            {
                var order = await _upvest.GetPortfolioOrderAsync(payment.UpvestOrderId, ct);
                if (order?.Status is not null && order.Status != payment.OrderStatus)
                {
                    payment.UpdateOrderStatus(order.Status);
                    await _payments.UpdateAsync(payment, ct);
                }
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not refresh payment order status (HTTP {Status}: {Detail}).", ex.StatusCode, ex.Message);
            }
        }

        return payments;
    }
}
