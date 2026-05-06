namespace DocumentIntake.Api.Services;

public sealed class LocalFileStorageService : IStorageService
{
    private const string StorageRoot = "./storage";

    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(StorageRoot);
    }

    public async Task<string> StoreAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var key = Path.Combine(StorageRoot, $"{Guid.NewGuid():N}-{fileName}");

        await using var file = new FileStream(key, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);

        _logger.LogInformation("Stored file {FileName} at {Key}", fileName, key);

        return key;
    }

    public Task<Stream> GetAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var stream = new FileStream(storageKey, FileMode.Open, FileAccess.Read, FileShare.Read);

        _logger.LogInformation("Retrieved file at {Key}", storageKey);

        return Task.FromResult<Stream>(stream);
    }
}
