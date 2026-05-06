using DocumentIntake.Api.Models;
using DocumentIntake.Api.Services;

namespace DocumentIntake.Api.Workers;

public sealed class DocumentProcessingWorker : BackgroundService
{
    private const int PreviewMaxChars = 500;

    private readonly IQueueService _queue;
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(
        IQueueService queue,
        IDocumentRepository repository,
        IStorageService storage,
        ILogger<DocumentProcessingWorker> logger)
    {
        _queue = queue;
        _repository = repository;
        _storage = storage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentProcessingWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            ProcessingMessage? message = null;

            try
            {
                message = await _queue.DequeueAsync(stoppingToken);

                if (message is null)
                    continue;

                await ProcessMessageAsync(message, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown — exit the loop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing message for document {DocumentId}",
                    message?.DocumentId);
            }
        }

        _logger.LogInformation("DocumentProcessingWorker stopped");
    }

    private async Task ProcessMessageAsync(ProcessingMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing document {DocumentId}", message.DocumentId);

        var document = await _repository.GetByIdAsync(message.DocumentId, cancellationToken);

        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} not found — skipping", message.DocumentId);
            return;
        }

        // --- Set Processing ---
        document.Status = DocumentStatus.Processing;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        document.AuditTrail.Add(new AuditEntry
        {
            Event = "Processing started",
            Timestamp = document.UpdatedAt
        });
        await _repository.SaveAsync(document, cancellationToken);

        try
        {
            // --- Fetch file from storage ---
            await using var stream = await _storage.GetAsync(document.StoragePath, cancellationToken);

            // --- Generate preview ---
            string preview;
            if (document.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(stream, leaveOpen: true);
                var buffer = new char[PreviewMaxChars];
                var charsRead = await reader.ReadAsync(buffer, 0, PreviewMaxChars);
                preview = new string(buffer, 0, charsRead);
            }
            else
            {
                // Binary content — measure size and return a fallback message
                var sizeBytes = CountStreamBytes(stream);
                preview = $"[Binary content - preview not available. Size: {sizeBytes} bytes]";
            }

            document.Preview = preview;
            document.Status = DocumentStatus.Processed;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            document.AuditTrail.Add(new AuditEntry
            {
                Event = "Processing complete",
                Timestamp = document.UpdatedAt,
                Detail = $"Preview generated ({preview.Length} characters)"
            });

            _logger.LogInformation("Document {DocumentId} processed successfully", document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process document {DocumentId}", document.Id);

            document.Status = DocumentStatus.Failed;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            document.AuditTrail.Add(new AuditEntry
            {
                Event = "Processing failed",
                Timestamp = document.UpdatedAt,
                Detail = ex.Message
            });
        }

        await _repository.SaveAsync(document, cancellationToken);
    }

    private static long CountStreamBytes(Stream stream)
    {
        if (stream.CanSeek)
            return stream.Length;

        // Non-seekable stream — drain it to count bytes
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            total += read;

        return total;
    }
}
