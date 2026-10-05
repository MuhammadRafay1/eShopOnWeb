using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class InvestingServiceTests
{
    private readonly IRepository<Enrolment> _enrolments = Substitute.For<IRepository<Enrolment>>();
    private readonly IRepository<Investment> _investments = Substitute.For<IRepository<Investment>>();
    private readonly IUpvestGateway _upvest = Substitute.For<IUpvestGateway>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();

    private InvestingService CreateService() => new(_enrolments, _investments, _upvest, _logger);

    [Theory]
    [InlineData(12.30, 70)]   // €12.30 -> set aside €0.70
    [InlineData(8.50, 50)]    // €8.50  -> €0.50
    [InlineData(12.00, 0)]    // whole euro -> nothing
    [InlineData(0.01, 99)]    // €0.01  -> €0.99
    [InlineData(100.00, 0)]   // whole euro -> nothing
    [InlineData(19.99, 1)]    // €19.99 -> €0.01
    public void RoundUpCents_rounds_to_next_whole_euro(decimal total, long expectedCents)
    {
        Assert.Equal(expectedCents, InvestingService.RoundUpCents(total));
    }

    [Fact]
    public async Task RecordPaidOrder_sets_aside_nothing_when_not_enrolled()
    {
        _enrolments.FirstOrDefaultAsync(Arg.Any<ISpecification<Enrolment>>(), Arg.Any<CancellationToken>())
            .Returns((Enrolment?)null);

        var setAside = await CreateService().RecordPaidOrderAsync("shopper", 12.30m, CancellationToken.None);

        Assert.Equal(0, setAside);
        await _investments.DidNotReceive().AddAsync(Arg.Any<Investment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordPaidOrder_sets_aside_nothing_when_enrolment_pending()
    {
        var pending = new Enrolment("shopper"); // starts AwaitingUserActivation -> not accepted
        _enrolments.FirstOrDefaultAsync(Arg.Any<ISpecification<Enrolment>>(), Arg.Any<CancellationToken>())
            .Returns(pending);

        var setAside = await CreateService().RecordPaidOrderAsync("shopper", 12.30m, CancellationToken.None);

        Assert.Equal(0, setAside);
        Assert.Equal(0, pending.PendingCents);
    }

    [Fact]
    public async Task RecordPaidOrder_sets_aside_round_up_for_accepted_investor()
    {
        var enrolment = ActiveEnrolment("shopper");
        _enrolments.FirstOrDefaultAsync(Arg.Any<ISpecification<Enrolment>>(), Arg.Any<CancellationToken>())
            .Returns(enrolment);

        var setAside = await CreateService().RecordPaidOrderAsync("shopper", 12.30m, CancellationToken.None);

        Assert.Equal(70, setAside);
        Assert.Equal(70, enrolment.PendingCents);
        await _enrolments.Received().UpdateAsync(enrolment, Arg.Any<CancellationToken>());
        await _investments.DidNotReceive().AddAsync(Arg.Any<Investment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordPaidOrder_invests_whole_balance_when_threshold_reached()
    {
        var enrolment = ActiveEnrolment("shopper");
        // Pre-load €9.60 so a €0.60 round-up (order of €0.40) crosses €10.
        for (var i = 0; i < 16; i++) enrolment.AddRoundUpAndMaybeInvest(60); // 16 * 0.60 = 9.60
        Assert.Equal(960, enrolment.PendingCents);

        _enrolments.FirstOrDefaultAsync(Arg.Any<ISpecification<Enrolment>>(), Arg.Any<CancellationToken>())
            .Returns(enrolment);
        Investment? captured = null;
        await _investments.AddAsync(Arg.Do<Investment>(i => captured = i), Arg.Any<CancellationToken>());

        var setAside = await CreateService().RecordPaidOrderAsync("shopper", 9.40m, CancellationToken.None); // round-up 0.60

        Assert.Equal(60, setAside);
        Assert.Equal(0, enrolment.PendingCents);                 // balance resets to zero after investing
        Assert.NotNull(captured);
        Assert.Equal(1020, captured!.AmountCents);               // whole €10.20 invested
        Assert.Equal(InvestmentStatus.Pending, captured.Status);
    }

    private static Enrolment ActiveEnrolment(string shopper)
    {
        var e = new Enrolment(shopper);
        e.RecordUser(System.Guid.NewGuid());
        e.RecordAccounts(System.Guid.NewGuid(), System.Guid.NewGuid());
        e.MarkActive();
        return e;
    }
}
