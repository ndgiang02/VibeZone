using System.ComponentModel.DataAnnotations;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using VibeZone.SharedKernel;

namespace VibeZone.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>S3 endpoint, e.g. http://minio:9000 locally. Omit for AWS S3.</summary>
    public string? ServiceUrl { get; set; }

    [Required]
    public string Region { get; set; } = "us-east-1";

    [Required]
    public string Bucket { get; set; } = string.Empty;

    [Required]
    public string AccessKey { get; set; } = string.Empty;

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Path-style addressing (bucket in the path) — required by MinIO.</summary>
    public bool ForcePathStyle { get; set; } = true;
}

/// <summary>
/// <see cref="IObjectStorage"/> over the AWS S3 SDK. Works with MinIO and other S3-compatible services:
/// the SDK's newer default request/response checksums are limited to "when required" for compatibility.
/// </summary>
internal sealed class S3ObjectStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IObjectStorage
{
    private string Bucket => options.Value.Bucket;

    public async Task PutAsync(string key, Stream content, long length, string contentType, CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        };
        request.Headers.ContentLength = length;
        await s3.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string key, SharedKernel.ByteRange? range, CancellationToken cancellationToken)
    {
        var request = new GetObjectRequest { BucketName = Bucket, Key = key };
        if (range is { } r)
        {
            request.ByteRange = new Amazon.S3.Model.ByteRange(r.From, r.To);
        }

        var response = await s3.GetObjectAsync(request, cancellationToken);
        return new ResponseOwningStream(response.ResponseStream, response);
    }

    public async Task DownloadToFileAsync(string key, string path, CancellationToken cancellationToken)
    {
        await using var source = await OpenReadAsync(key, null, cancellationToken);
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await source.CopyToAsync(target, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await s3.DeleteObjectAsync(Bucket, key, cancellationToken);

    /// <summary>Disposes the SDK response (and its HTTP connection) together with the body stream.</summary>
    private sealed class ResponseOwningStream(Stream inner, IDisposable owner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                owner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

/// <summary>Ready only if the bucket is reachable with the application's (least-privilege) credentials.</summary>
internal sealed class StorageHealthCheck(IAmazonS3 s3, IOptions<StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = options.Value.Bucket, MaxKeys = 1 }, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is AmazonServiceException or HttpRequestException or AmazonClientException)
        {
            return HealthCheckResult.Unhealthy("Object storage unreachable.");
        }
    }
}

public static class StorageExtensions
{
    public const string HealthCheckName = "storage";

    public static IServiceCollection AddVibeZoneStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var config = new AmazonS3Config
            {
                ForcePathStyle = o.ForcePathStyle,
                AuthenticationRegion = o.Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };
            if (!string.IsNullOrWhiteSpace(o.ServiceUrl))
            {
                config.ServiceURL = o.ServiceUrl;
            }
            else
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(o.Region);
            }

            return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), config);
        });
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        return services;
    }

    public static IHealthChecksBuilder AddStorageCheck(this IHealthChecksBuilder builder, params string[] tags) =>
        builder.AddCheck<StorageHealthCheck>(HealthCheckName, tags: tags, timeout: TimeSpan.FromSeconds(5));
}
