using Amazon.S3;
using Amazon.S3.Model;

namespace DocumentIntake.Api.Services;

public sealed class S3StorageService : IStorageService
{
    private const string BucketName = "document-intake";

    private readonly IAmazonS3 _s3;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IAmazonS3 s3, ILogger<S3StorageService> logger)
    {
        _s3 = s3;
        _logger = logger;
    }

    public async Task<string> StoreAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var key = $"{Guid.NewGuid():N}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _s3.PutObjectAsync(request, cancellationToken);

        _logger.LogInformation("Stored file {FileName} at S3 key {Key}", fileName, key);

        return key;
    }

    public async Task<Stream> GetAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var response = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = BucketName,
            Key = storageKey
        }, cancellationToken);

        _logger.LogInformation("Retrieved file at S3 key {Key}", storageKey);

        return response.ResponseStream;
    }
}
