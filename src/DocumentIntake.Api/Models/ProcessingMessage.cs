namespace DocumentIntake.Api.Models;

public sealed class ProcessingMessage
{
    public Guid DocumentId { get; init; }
    public string SourceDocumentId { get; init; } = string.Empty;
    public string Action { get; init; } = "generate-preview";
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;
}
