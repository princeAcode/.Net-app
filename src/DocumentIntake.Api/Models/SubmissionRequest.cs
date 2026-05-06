using Microsoft.AspNetCore.Mvc;

namespace DocumentIntake.Api.Models;

public sealed class SubmissionRequest
{
    [FromForm(Name = "sourceDocumentId")]
    public string SourceDocumentId { get; set; } = string.Empty;

    [FromForm(Name = "provider")]
    public string Provider { get; set; } = string.Empty;

    [FromForm(Name = "title")]
    public string Title { get; set; } = string.Empty;

    [FromForm(Name = "jurisdiction")]
    public string Jurisdiction { get; set; } = string.Empty;

    [FromForm(Name = "categories")]
    public List<string> Categories { get; set; } = [];

    [FromForm(Name = "tags")]
    public List<string> Tags { get; set; } = [];

    [FromForm(Name = "preview")]
    public string? Preview { get; set; }

    [FromForm(Name = "file")]
    public IFormFile File { get; set; } = null!;
}
