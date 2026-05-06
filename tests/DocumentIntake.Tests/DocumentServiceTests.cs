using System.Text;
using DocumentIntake.Api.Models;
using DocumentIntake.Api.Services;
using DocumentIntake.Api.Workers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentIntake.Tests;

public sealed class DocumentServiceTests
{
    // -------------------------------------------------------------------------
    // Test 1 — Deduplication
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_WhenSameDocumentSubmittedTwice_DeduplicatesAndRecordsAuditEntry()
    {
        // Arrange
        var storageMock = new Mock<IStorageService>();
        storageMock
            .Setup(s => s.StoreAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("bucket/stored-key.txt");

        var queueMock = new Mock<IQueueService>();
        queueMock
            .Setup(q => q.EnqueueAsync(It.IsAny<ProcessingMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var repository = new InMemoryDocumentRepository();
        var service = new DocumentService(
            repository,
            storageMock.Object,
            queueMock.Object,
            Mock.Of<ILogger<DocumentService>>());

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.ContentType).Returns("text/plain");
        fileMock.Setup(f => f.FileName).Returns("contract.txt");

        var request = new SubmissionRequest
        {
            SourceDocumentId = "SRC-001",
            Provider = "acme",
            Title = "Contract",
            Jurisdiction = "US",
            File = fileMock.Object
        };

        // Act — submit the same document twice with different file content
        var firstResult = await service.SubmitAsync(
            request, new MemoryStream(Encoding.UTF8.GetBytes("Version A content")));

        var secondResult = await service.SubmitAsync(
            request, new MemoryStream(Encoding.UTF8.GetBytes("Version B content")));

        // Assert — both calls returned the same document (only 1 was ever stored)
        Assert.Equal(firstResult.Id, secondResult.Id);
        Assert.Contains(
            secondResult.AuditTrail,
            e => e.Event == "Duplicate submission received");
    }

    // -------------------------------------------------------------------------
    // Test 2 — Happy path: worker processes a queued document
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Worker_WhenDocumentIsQueued_SetsStatusToProcessedAndGeneratesPreview()
    {
        // Arrange
        const string textContent = "Hello this is a test document for preview generation";

        var storageMock = new Mock<IStorageService>();
        storageMock
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<Stream>(
                new MemoryStream(Encoding.UTF8.GetBytes(textContent))));

        var repository = new InMemoryDocumentRepository();

        var document = new Document
        {
            SourceDocumentId = "SRC-002",
            Provider = "acme",
            Title = "Worker Test Doc",
            ContentType = "text/plain",
            FileName = "test.txt",
            StoragePath = "abc123/test.txt",
            Status = DocumentStatus.Queued
        };
        document.AuditTrail.Add(new AuditEntry
        {
            Event = "Queued",
            Timestamp = DateTimeOffset.UtcNow,
            Detail = "Sent to processing queue"
        });
        await repository.SaveAsync(document);

        var queue = new InMemoryQueueService();
        await queue.EnqueueAsync(new ProcessingMessage
        {
            DocumentId = document.Id,
            SourceDocumentId = document.SourceDocumentId
        });

        var worker = new DocumentProcessingWorker(
            queue,
            repository,
            storageMock.Object,
            Mock.Of<ILogger<DocumentProcessingWorker>>());

        // Act — start worker, allow time to process the single queued message, then stop
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(500);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        var processed = await repository.GetByIdAsync(document.Id);

        Assert.NotNull(processed);
        Assert.Equal(DocumentStatus.Processed, processed.Status);
        Assert.False(string.IsNullOrWhiteSpace(processed.Preview));
        Assert.Contains(processed.AuditTrail, e => e.Event == "Processing started");
        Assert.Contains(processed.AuditTrail, e => e.Event == "Processing complete");
    }
}
