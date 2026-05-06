using DocumentIntake.Api.Models;

namespace DocumentIntake.Api.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storage;
    private readonly IQueueService _queue;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository repository,
        IStorageService storage,
        IQueueService queue,
        ILogger<DocumentService> logger)
    {
        _repository = repository;
        _storage = storage;
        _queue = queue;
        _logger = logger;
    }

    public async Task<Document> SubmitAsync(SubmissionRequest request, Stream fileContent, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.FindByDeduplicationKeyAsync(
            request.Provider, request.SourceDocumentId, cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Duplicate submission for provider={Provider} sourceDocumentId={SourceDocumentId}",
                request.Provider, request.SourceDocumentId);

            existing.AuditTrail.Add(new AuditEntry
            {
                Event = "Duplicate submission received",
                Timestamp = DateTimeOffset.UtcNow,
                Detail = $"Re-submission of provider={request.Provider}, sourceDocumentId={request.SourceDocumentId}"
            });

            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.SaveAsync(existing, cancellationToken);

            return existing;
        }

        var now = DateTimeOffset.UtcNow;

        var document = new Document
        {
            SourceDocumentId = request.SourceDocumentId,
            Provider = request.Provider,
            Title = request.Title,
            Jurisdiction = request.Jurisdiction,
            Categories = request.Categories,
            Tags = request.Tags,
            ContentType = request.File.ContentType,
            FileName = request.File.FileName,
            Preview = request.Preview,
            ReceivedAt = now,
            UpdatedAt = now,
            Status = DocumentStatus.Received
        };

        document.AuditTrail.Add(new AuditEntry
        {
            Event = "Received",
            Timestamp = now,
            Detail = $"Document received from provider={request.Provider}"
        });

        var storageKey = await _storage.StoreAsync(
            request.File.FileName, fileContent, request.File.ContentType, cancellationToken);

        document.StoragePath = storageKey;
        document.Status = DocumentStatus.Stored;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        document.AuditTrail.Add(new AuditEntry
        {
            Event = "Stored",
            Timestamp = document.UpdatedAt,
            Detail = $"File stored at S3 key: {storageKey}"
        });

        await _queue.EnqueueAsync(new ProcessingMessage
        {
            DocumentId = document.Id,
            SourceDocumentId = document.SourceDocumentId
        }, cancellationToken);

        document.Status = DocumentStatus.Queued;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        document.AuditTrail.Add(new AuditEntry
        {
            Event = "Queued",
            Timestamp = document.UpdatedAt,
            Detail = "Sent to processing queue"
        });

        await _repository.SaveAsync(document, cancellationToken);

        _logger.LogInformation("Document {Id} queued for processing", document.Id);

        return document;
    }
}
