namespace DocumentIntake.Api.Models;

public sealed class Document
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string SourceDocumentId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Jurisdiction { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Received;
    public string? Preview { get; set; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<AuditEntry> AuditTrail { get; set; } = [];
}
