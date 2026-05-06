namespace DocumentIntake.Api.Models;

public sealed class AuditEntry
{
    public string Event { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public string? Detail { get; init; }
}
