using DocumentIntake.Api.Models;
using DocumentIntake.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentIntake.Api.Controllers;

[ApiController]
[Route("documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IDocumentRepository _repository;

    public DocumentsController(IDocumentService documentService, IDocumentRepository repository)
    {
        _documentService = documentService;
        _repository = repository;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(Document), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromForm] SubmissionRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest("A non-empty file is required.");

        await using var stream = request.File.OpenReadStream();
        var document = await _documentService.SubmitAsync(request, stream, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Document), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var document = await _repository.GetByIdAsync(id, cancellationToken);

        return document is null ? NotFound() : Ok(document);
    }

    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        var document = await _repository.GetByIdAsync(id, cancellationToken);

        if (document is null)
            return NotFound();

        return Ok(new
        {
            documentId = document.Id,
            sourceDocumentId = document.SourceDocumentId,
            status = document.Status.ToString(),
            updatedAt = document.UpdatedAt,
            previewSizeBytes = document.Preview is not null
                ? System.Text.Encoding.UTF8.GetByteCount(document.Preview)
                : (int?)null,
            auditTrail = document.AuditTrail.Select(e => new
            {
                @event = e.Event,
                timestamp = e.Timestamp,
                detail = e.Detail
            })
        });
    }

    [HttpGet("{id:guid}/preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetPreview(Guid id, CancellationToken cancellationToken)
    {
        var document = await _repository.GetByIdAsync(id, cancellationToken);

        if (document is null)
            return NotFound();

        if (document.Status != DocumentStatus.Processed)
            return Conflict(new { message = "Preview not yet available" });

        return Ok(new
        {
            documentId = document.Id,
            preview = document.Preview,
            status = document.Status.ToString()
        });
    }
}
