using DocumentIntake.Api.Models;

namespace DocumentIntake.Api.Services;

public interface IDocumentRepository
{
    Task<Document?> FindByDeduplicationKeyAsync(string provider, string sourceDocumentId, CancellationToken cancellationToken = default);
    Task<Document> SaveAsync(Document document, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
