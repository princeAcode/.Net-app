using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using DocumentIntake.Api.Services;
using DocumentIntake.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

// AWS / LocalStack configuration
var awsCredentials = new BasicAWSCredentials("test", "test");

builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
    awsCredentials,
    new AmazonS3Config
    {
        RegionEndpoint = RegionEndpoint.USEast1,
        ServiceURL = "http://localhost:4566",
        ForcePathStyle = true
    }));

builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(
    awsCredentials,
    new AmazonSQSConfig
    {
        RegionEndpoint = RegionEndpoint.USEast1,
        ServiceURL = "http://localhost:4566"
    }));

// Application services
// IDocumentRepository is singleton — it IS the in-memory store
builder.Services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();

// IQueueService: swap to SqsQueueService when SQS is available
builder.Services.AddSingleton<IQueueService, InMemoryQueueService>();

// Storage and document services are stateless — safe as singletons
// (required because DocumentProcessingWorker is a BackgroundService / singleton)
builder.Services.AddSingleton<IStorageService, LocalFileStorageService>();
builder.Services.AddSingleton<IDocumentService, DocumentService>();

// Background worker
builder.Services.AddHostedService<DocumentProcessingWorker>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
