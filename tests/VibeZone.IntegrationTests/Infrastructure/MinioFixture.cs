using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace VibeZone.IntegrationTests.Infrastructure;

/// <summary>Real S3-compatible object store (same image as docker-compose.yml) with an empty private bucket.</summary>
public sealed class MinioFixture : IAsyncLifetime
{
    public const string Bucket = "vibezone-test";
    private const int ApiPort = 9000;
    private const string User = "vibezone-test";
    private const string Password = "vibezone-test-secret";

    private readonly IContainer _container = new ContainerBuilder("pgsty/minio:RELEASE.2026-08-04T00-00-00Z")
        .WithCommand("server", "/data")
        .WithEnvironment("MINIO_ROOT_USER", User)
        .WithEnvironment("MINIO_ROOT_PASSWORD", Password)
        .WithPortBinding(ApiPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(ApiPort).ForPath("/minio/health/live")))
        .Build();

    /// <summary>Settings for <see cref="ApiFactory"/> pointing the API at this container.</summary>
    public IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>
    {
        ["Storage:ServiceUrl"] = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(ApiPort)}",
        ["Storage:Bucket"] = Bucket,
        ["Storage:AccessKey"] = User,
        ["Storage:SecretKey"] = Password,
    };

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var result = await _container.ExecAsync(
            ["sh", "-c", $"mc alias set local http://127.0.0.1:{ApiPort} {User} {Password} >/dev/null && mc mb --ignore-existing local/{Bucket}"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Creating the test bucket failed: {result.Stderr}");
        }
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
