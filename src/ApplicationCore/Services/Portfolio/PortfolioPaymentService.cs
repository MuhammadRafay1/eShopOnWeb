using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;

public class PortfolioPaymentService : IPortfolioPaymentService
{
    private readonly IRepository<PortfolioPayment> _payments;
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestInvestmentGateway _gateway;
    private readonly IAppLogger<PortfolioPaymentService> _logger;

    public PortfolioPaymentService(
        IRepository<PortfolioPayment> payments,
        IRepository<Investor> investors,
        IUpvestInvestmentGateway gateway,
        IAppLogger<PortfolioPaymentService> logger)
    {
        _payments = payments;
        _investors = investors;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<PaymentView> PayInAsync(string shopperUserName, decimal amountEuros, CancellationToken ct)
    {
        if (amountEuros <= 0)
        {
            throw new PortfolioValidationException("Amount must be greater than zero.");
        }

        var investor = await _investors.FirstOrDefaultAsync(new InvestorByUserNameSpecification(shopperUserName), ct);
        if (investor is null || !investor.CanInvest)
        {
            throw new PortfolioStateException("You must be an accepted investor with a ready account before paying in.");
        }
        if (investor.SelectedModelPortfolioId is null)
        {
            throw new PortfolioStateException("You must choose a model portfolio before paying in.");
        }

        // Duplicate-claim: write the payment record (with its idempotency keys) before any Upvest call.
        var payment = new PortfolioPayment(shopperUserName, amountEuros);
        payment = await _payments.AddAsync(payment, ct);

        var command = new PayInCommand(
            investor.UpvestUserId!.Value,
            investor.UpvestAccountGroupId!.Value,
            investor.UpvestAccountId!.Value,
            amountEuros,
            payment.ClientReference,
            payment.TopupIdempotencyKey,
            payment.BankTransactionIdempotencyKey,
            payment.OrderIdempotencyKey);

        try
        {
            var result = await _gateway.PayInAsync(command, ct);
            payment.SetTopup(result.TopupId);
            if (result.OrderId is not null)
            {
                payment.SetOrder(result.OrderId.Value);
            }
            payment.SetStatus(result.Status);
            await _payments.UpdateAsync(payment, ct);
            _logger.LogInformation("Pay-in {PaymentId} processed; status {Status}.", payment.PaymentKey, result.Status);
        }
        catch (UpvestGatewayException ex)
        {
            // A definite business rejection fails the payment; anything else (scope, transport, unknown)
            // leaves it pending for reconciliation on a later read.
            var status = ex.OutcomeUnknown ? PaymentStatus.Pending : MapFailure(ex.StatusCode);
            payment.SetStatus(status);
            await _payments.UpdateAsync(payment, ct);
            _logger.LogWarning("Pay-in {PaymentId} could not be completed (status {Status}); recorded as {Mapped}.",
                payment.PaymentKey, ex.StatusCode, status);
        }

        return ToView(payment);
    }

    public async Task<IReadOnlyList<PaymentView>> ListPaymentsAsync(string shopperUserName, CancellationToken ct)
    {
        var payments = await _payments.ListAsync(new PaymentsByUserSpecification(shopperUserName), ct);

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending &&
                                                    (p.UpvestTopupId is not null || p.UpvestOrderId is not null)))
        {
            try
            {
                var status = await _gateway.GetPaymentStatusAsync(payment.UpvestTopupId, payment.UpvestOrderId, ct);
                if (status != payment.Status)
                {
                    payment.SetStatus(status);
                    await _payments.UpdateAsync(payment, ct);
                }
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not reconcile payment {PaymentId} (status {Status}).", payment.PaymentKey, ex.StatusCode);
            }
        }

        return payments.Select(ToView).ToList();
    }

    private static PaymentStatus MapFailure(HttpStatusCode? statusCode) =>
        statusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict
            ? PaymentStatus.Failed
            : PaymentStatus.Pending;

    private static PaymentView ToView(PortfolioPayment p) => new(p.PaymentKey, p.Amount, p.Status);
}
