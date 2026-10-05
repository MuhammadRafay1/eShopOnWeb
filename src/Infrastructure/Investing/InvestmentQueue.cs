using System.Threading.Channels;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>In-process queue of investments awaiting funding + order placement by the background processor.</summary>
public sealed class InvestmentQueue : IInvestmentQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public void Enqueue(int investmentId) => _channel.Writer.TryWrite(investmentId);

    public ChannelReader<int> Reader => _channel.Reader;
}
