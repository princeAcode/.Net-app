using DocumentIntake.Api.Models;

namespace DocumentIntake.Api.Services;

public interface IDocumentService
{
    Task<Document> SubmitAsync(SubmissionRequest request, Stream fileContent, CancellationToken cancellationToken = default);
}
