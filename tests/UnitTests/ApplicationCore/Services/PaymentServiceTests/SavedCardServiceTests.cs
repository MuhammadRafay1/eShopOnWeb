using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class SavedCardServiceTests
{
    private const string Buyer = "buyer@example.com";

    private readonly IRepository<SavedCard> _cards = Substitute.For<IRepository<SavedCard>>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly IAppLogger<SavedCardService> _logger = Substitute.For<IAppLogger<SavedCardService>>();

    private SavedCardService CreateService() => new(_cards, _gateway, new PaymentLock(), _logger);

    private static CardInput Card() => new("4111111111111111", "2030-01", "123", "Demo", null);

    private void StoreCards(params SavedCard[] cards) =>
        _cards.ListAsync(Arg.Any<SavedCardsByBuyerSpecification>(), Arg.Any<CancellationToken>()).Returns(new List<SavedCard>(cards));

    [Fact]
    public async Task SaveCardAsync_vaults_and_stores_safe_description()
    {
        StoreCards();
        _gateway.VaultCardAsync(Arg.Any<VaultCardGatewayRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VaultedCardResult("vault-1", "cust-1", "VISA", "1111", "2030-01", "Demo"));
        _cards.AddAsync(Arg.Any<SavedCard>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<SavedCard>());

        var view = await CreateService().SaveCardAsync(Buyer, Card());

        Assert.Equal("VISA", view.Brand);
        Assert.Equal("1111", view.LastFourDigits);
        await _cards.Received().AddAsync(Arg.Is<SavedCard>(c => c.PayPalVaultId == "vault-1" && c.BuyerId == Buyer), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCardAsync_reuses_existing_customer_id()
    {
        StoreCards(new SavedCard(Buyer, "vault-0", "cust-1", "VISA", "0000", "2029-01", "Demo"));
        _gateway.VaultCardAsync(Arg.Any<VaultCardGatewayRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VaultedCardResult("vault-2", "cust-1", "VISA", "1111", "2030-01", "Demo"));
        _cards.AddAsync(Arg.Any<SavedCard>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<SavedCard>());

        await CreateService().SaveCardAsync(Buyer, Card());

        await _gateway.Received().VaultCardAsync(
            Arg.Is<VaultCardGatewayRequest>(r => r.ExistingPayPalCustomerId == "cust-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCardAsync_dedupes_by_vault_id()
    {
        var existing = new SavedCard(Buyer, "vault-1", "cust-1", "VISA", "1111", "2030-01", "Demo");
        StoreCards(existing);
        _gateway.VaultCardAsync(Arg.Any<VaultCardGatewayRequest>(), Arg.Any<CancellationToken>())
            .Returns(new VaultedCardResult("vault-1", "cust-1", "VISA", "1111", "2030-01", "Demo"));

        await CreateService().SaveCardAsync(Buyer, Card());

        await _cards.DidNotReceive().AddAsync(Arg.Any<SavedCard>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCardAsync_gateway_failure_propagates()
    {
        StoreCards();
        _gateway.VaultCardAsync(Arg.Any<VaultCardGatewayRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<VaultedCardResult>(new PaymentGatewayException("unavailable", null, false)));

        await Assert.ThrowsAsync<PaymentGatewayException>(() => CreateService().SaveCardAsync(Buyer, Card()));
    }

    [Fact]
    public async Task DeleteCardAsync_of_another_buyer_is_not_found()
    {
        _cards.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(new SavedCard("other@example.com", "vault-1", "cust-1", "VISA", "1111", "2030-01", "X"));

        await Assert.ThrowsAsync<PaymentNotFoundException>(() => CreateService().DeleteCardAsync(Buyer, 5));
        await _gateway.DidNotReceive().DeleteVaultedCardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCardAsync_deletes_from_gateway_and_repo()
    {
        var card = new SavedCard(Buyer, "vault-1", "cust-1", "VISA", "1111", "2030-01", "Demo");
        _cards.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(card);

        await CreateService().DeleteCardAsync(Buyer, 5);

        await _gateway.Received().DeleteVaultedCardAsync("vault-1", Arg.Any<CancellationToken>());
        await _cards.Received().DeleteAsync(card, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCardAsync_swallows_404_from_gateway()
    {
        var card = new SavedCard(Buyer, "vault-1", "cust-1", "VISA", "1111", "2030-01", "Demo");
        _cards.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(card);
        _gateway.DeleteVaultedCardAsync("vault-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PaymentGatewayException("gone", 404, isCallerError: true)));

        await CreateService().DeleteCardAsync(Buyer, 5);

        await _cards.Received().DeleteAsync(card, Arg.Any<CancellationToken>());
    }
}
