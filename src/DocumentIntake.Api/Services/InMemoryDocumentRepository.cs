using System.Collections.Concurrent;
using DocumentIntake.Api.Models;

namespace DocumentIntake.Api.Services;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, Document> _store = new();

    public Task<Document?> FindByDeduplicationKeyAsync(string provider, string sourceDocumentId, CancellationToken cancellationToken = default)
    {
        var match = _store.Values.FirstOrDefault(d =>
            string.Equals(d.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(d.SourceDocumentId, sourceDocumentId, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match);
    }

    public Task<Document> SaveAsync(Document document, CancellationToken cancellationToken = default)
    {
        _store[document.Id] = document;
        return Task.FromResult(document);
    }

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var document);
        return Task.FromResult(document);
    }
}
