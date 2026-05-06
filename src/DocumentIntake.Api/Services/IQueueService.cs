using DocumentIntake.Api.Models;

namespace DocumentIntake.Api.Services;

public interface IQueueService
{
    Task EnqueueAsync(ProcessingMessage message, CancellationToken cancellationToken = default);
    Task<ProcessingMessage?> DequeueAsync(CancellationToken cancellationToken = default);
}
