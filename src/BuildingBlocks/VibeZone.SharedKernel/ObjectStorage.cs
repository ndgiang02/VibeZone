namespace VibeZone.SharedKernel;

/// <summary>Inclusive byte range, as in HTTP <c>Range: bytes=From-To</c>.</summary>
public readonly record struct ByteRange(long From, long To)
{
    public long Length => To - From + 1;
}

/// <summary>
/// Private object storage (S3-compatible). Keys are always server-generated; callers never pass
/// user-supplied names. Implemented in VibeZone.Infrastructure (ADR-0003).
/// </summary>
public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, long length, string contentType, CancellationToken cancellationToken);

    /// <summary>Opens the object (or a range of it) for streaming. Dispose the stream to release the connection.</summary>
    Task<Stream> OpenReadAsync(string key, ByteRange? range, CancellationToken cancellationToken);

    Task DownloadToFileAsync(string key, string path, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
