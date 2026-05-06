namespace DocumentIntake.Api.Services;

public interface IStorageService
{
    Task<string> StoreAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetAsync(string storageKey, CancellationToken cancellationToken = default);
}
