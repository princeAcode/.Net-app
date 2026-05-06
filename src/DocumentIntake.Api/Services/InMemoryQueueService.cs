using System.Threading.Channels;
using DocumentIntake.Api.Models;

namespace DocumentIntake.Api.Services;

public sealed class InMemoryQueueService : IQueueService
{
    private readonly Channel<ProcessingMessage> _channel = Channel.CreateUnbounded<ProcessingMessage>(
        new UnboundedChannelOptions { SingleReader = true });

    public async Task EnqueueAsync(ProcessingMessage message, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
    }

    public async Task<ProcessingMessage?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
