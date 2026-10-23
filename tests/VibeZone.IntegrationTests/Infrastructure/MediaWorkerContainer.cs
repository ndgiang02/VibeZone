using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Npgsql;

namespace VibeZone.IntegrationTests.Infrastructure;

/// <summary>
/// The real worker image (src/VibeZone.Worker/Dockerfile, FFmpeg from the distribution), built once per run
/// and pointed at the test Postgres and MinIO through the Docker host.
/// </summary>
public static class MediaWorkerContainer
{
    private const string DockerHost = "host.docker.internal";

    private static readonly IFutureDockerImage Image = new ImageFromDockerfileBuilder()
        .WithDockerfileDirectory(CommonDirectoryPath.GetGitDirectory(), string.Empty)
        .WithDockerfile("src/VibeZone.Worker/Dockerfile")
        .WithName("vibezone-worker:integration-tests")
        .WithCleanUp(false)
        .Build();

    private static readonly Lazy<Task> Built = new(() => Image.CreateAsync());

    public static async Task<IContainer> StartAsync(string connectionString, MinioFixture storage, TimeSpan? lease = null)
    {
        await Built.Value;
        var database = new NpgsqlConnectionStringBuilder(connectionString) { Host = DockerHost }.ConnectionString;
        var container = new ContainerBuilder(Image.FullName)
            .WithExtraHost(DockerHost, "host-gateway")
            .WithEnvironment("ConnectionStrings__Database", database)
            .WithEnvironment("Storage__ServiceUrl", $"http://{DockerHost}:{storage.MappedPort}")
            .WithEnvironment("Storage__Bucket", MinioFixture.Bucket)
            .WithEnvironment("Storage__AccessKey", MinioFixture.AccessKey)
            .WithEnvironment("Storage__SecretKey", MinioFixture.SecretKey)
            .WithEnvironment("Media__PollInterval", "00:00:00.500")
            .WithEnvironment("Media__LeaseDuration", (lease ?? TimeSpan.FromMinutes(1)).ToString("c"))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Media worker .* started"))
            .Build();
        await container.StartAsync();
        return container;
    }

    /// <summary>SIGKILL (no graceful shutdown), like a crashed or OOM-killed worker.</summary>
    public static async Task KillAsync(IContainer container)
    {
        using var docker = Process.Start(new ProcessStartInfo("docker") { ArgumentList = { "kill", container.Id }, RedirectStandardOutput = true, RedirectStandardError = true })!;
        await docker.WaitForExitAsync();
        Assert.Equal(0, docker.ExitCode);
    }
}
