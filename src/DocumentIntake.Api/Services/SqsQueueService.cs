using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using DocumentIntake.Api.Models;

namespace DocumentIntake.Api.Services;

public sealed class SqsQueueService : IQueueService
{
    private const string QueueUrl = "http://localhost:4566/000000000000/document-processing";

    private readonly IAmazonSQS _sqs;
    private readonly ILogger<SqsQueueService> _logger;

    public SqsQueueService(IAmazonSQS sqs, ILogger<SqsQueueService> logger)
    {
        _sqs = sqs;
        _logger = logger;
    }

    public async Task EnqueueAsync(ProcessingMessage message, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.Serialize(message);

        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = QueueUrl,
            MessageBody = body
        }, cancellationToken);

        _logger.LogInformation("Enqueued message for document {DocumentId}", message.DocumentId);
    }

    public async Task<ProcessingMessage?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = QueueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 5
        }, cancellationToken);

        var sqsMessage = response.Messages.FirstOrDefault();
        if (sqsMessage is null)
            return null;

        await _sqs.DeleteMessageAsync(QueueUrl, sqsMessage.ReceiptHandle, cancellationToken);

        var message = JsonSerializer.Deserialize<ProcessingMessage>(sqsMessage.Body);

        _logger.LogInformation("Dequeued message for document {DocumentId}", message?.DocumentId);

        return message;
    }
}
