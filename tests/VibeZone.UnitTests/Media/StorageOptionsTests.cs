using System.ComponentModel.DataAnnotations;
using VibeZone.Infrastructure.Storage;

namespace VibeZone.UnitTests.Media;

public sealed class StorageOptionsTests
{
    private static bool IsValid(StorageOptions options) =>
        Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);

    [Fact]
    public void Aws_without_keys_uses_the_instance_role()
    {
        Assert.True(IsValid(new StorageOptions { Bucket = "media", Region = "ap-southeast-1", ForcePathStyle = false }));
    }

    [Fact]
    public void Minio_requires_static_keys()
    {
        Assert.False(IsValid(new StorageOptions { Bucket = "media", ServiceUrl = "http://minio:9000" }));
        Assert.True(IsValid(new StorageOptions { Bucket = "media", ServiceUrl = "http://minio:9000", AccessKey = "app", SecretKey = "secret123" }));
    }

    [Fact]
    public void Half_a_key_pair_is_rejected()
    {
        Assert.False(IsValid(new StorageOptions { Bucket = "media", AccessKey = "app" }));
        Assert.False(IsValid(new StorageOptions { Bucket = "media", SecretKey = "secret123" }));
    }
}
