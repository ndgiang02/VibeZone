using System.Security.Cryptography;
using VibeZone.Modules.Media.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Media.Application;

/// <summary>
/// An upload body copied to a server-named temp file while hashing, stopping as soon as it exceeds the limit.
/// Disposing deletes the file.
/// </summary>
public sealed class SpooledUpload : IDisposable
{
    private SpooledUpload(string path) => Path = path;

    public string Path { get; }

    public long Length { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    /// <summary>The first bytes, for the signature sniff.</summary>
    public byte[] Head { get; private set; } = [];

    /// <summary>True if reading stopped because the body was longer than the limit.</summary>
    public bool TooLarge { get; private set; }

    public static async Task<SpooledUpload> CreateAsync(Stream body, string directory, long maxBytes, CancellationToken cancellationToken)
    {
        var spooled = new SpooledUpload(System.IO.Path.Combine(directory, $"{EntityId.New():N}.upload"));
        try
        {
            await spooled.CopyAsync(body, maxBytes, cancellationToken);
            return spooled;
        }
        catch
        {
            spooled.Dispose();
            throw;
        }
    }

    private async Task CopyAsync(Stream body, long maxBytes, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var head = new byte[AudioSignature.BytesNeeded];
        var headLength = 0;
        long total = 0;
        var buffer = new byte[81920];

        await using var file = new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                Length = total;
                TooLarge = true;
                return;
            }

            if (headLength < head.Length)
            {
                var take = Math.Min(head.Length - headLength, read);
                Array.Copy(buffer, 0, head, headLength, take);
                headLength += take;
            }

            hash.AppendData(buffer, 0, read);
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        Length = total;
        Sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
        Head = head[..headLength];
    }

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (IOException)
        {
            // Temp directory is cleaned by the OS/container lifecycle; nothing sensitive is lost.
        }
    }
}
