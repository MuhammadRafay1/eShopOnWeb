using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.SavedPaymentMethodServiceTests;

public class SavedPaymentMethodServiceTests
{
    private const string BuyerId = "buyer-1";
    private const string OtherBuyerId = "buyer-2";

    private readonly IPayPalPaymentGateway _gateway = Substitute.For<IPayPalPaymentGateway>();
    private readonly IRepository<SavedPaymentMethod> _repo = Substitute.For<IRepository<SavedPaymentMethod>>();
    private readonly IAppLogger<SavedPaymentMethodService> _logger = Substitute.For<IAppLogger<SavedPaymentMethodService>>();

    private SavedPaymentMethodService CreateService() => new(_gateway, _repo, _logger);

    private static CardDetails Card() => new(
        "4111111111111111", 12, 2030, "123", "Test Shopper",
        "1 Test St", null, "Redmond", "WA", "98052", "US");

    [Fact]
    public async Task SaveCard_VaultsAndPersistsSafeMetadata()
    {
        _gateway.CreateVaultedCardAsync(Arg.Any<string>(), BuyerId, Arg.Any<CardDetails>(), Arg.Any<CancellationToken>())
            .Returns(new VaultedCardResult("VAULT1", "VISA", "1111", "2030-12"));
        _repo.AddAsync(Arg.Any<SavedPaymentMethod>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<SavedPaymentMethod>());

        var saved = await CreateService().SaveCardAsync(BuyerId, Card(), CancellationToken.None);

        Assert.Equal(BuyerId, saved.BuyerId);
        Assert.Equal("VAULT1", saved.PayPalVaultId);
        Assert.Equal("1111", saved.LastFour);
        Assert.Equal("VISA", saved.CardBrand);
    }

    [Fact]
    public async Task Delete_Owned_RevokesPayPalThenDeletesLocal()
    {
        var card = new SavedPaymentMethod(BuyerId, "VAULT1", "VISA", "1111", "2030-12", "Me");
        _repo.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns(card);

        await CreateService().DeleteAsync(BuyerId, 3, CancellationToken.None);

        Received.InOrder(() =>
        {
            _gateway.DeleteVaultedCardAsync("VAULT1", Arg.Any<CancellationToken>());
            _repo.DeleteAsync(card, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Delete_NotOwned_Throws404_AndDoesNotCallPayPal()
    {
        var othersCard = new SavedPaymentMethod(OtherBuyerId, "VAULT1", "VISA", "1111", "2030-12", "X");
        _repo.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns(othersCard);

        await Assert.ThrowsAsync<PaymentMethodNotFoundException>(() =>
            CreateService().DeleteAsync(BuyerId, 3, CancellationToken.None));

        await _gateway.DidNotReceive().DeleteVaultedCardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().DeleteAsync(Arg.Any<SavedPaymentMethod>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_PayPalRevokeFails_DoesNotDeleteLocalRow()
    {
        var card = new SavedPaymentMethod(BuyerId, "VAULT1", "VISA", "1111", "2030-12", "Me");
        _repo.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns(card);
        _gateway.DeleteVaultedCardAsync("VAULT1", Arg.Any<CancellationToken>()).Throws(new PaymentGatewayException("boom"));

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            CreateService().DeleteAsync(BuyerId, 3, CancellationToken.None));

        await _repo.DidNotReceive().DeleteAsync(Arg.Any<SavedPaymentMethod>(), Arg.Any<CancellationToken>());
    }
}
