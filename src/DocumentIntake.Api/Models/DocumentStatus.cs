namespace DocumentIntake.Api.Models;

public enum DocumentStatus
{
    Received,
    Stored,
    Queued,
    Processing,
    Processed,
    Failed
}
