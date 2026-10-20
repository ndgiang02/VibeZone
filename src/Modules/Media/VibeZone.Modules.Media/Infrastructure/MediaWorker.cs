using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeZone.Modules.Media.Application;
using VibeZone.Modules.Media.Domain;

namespace VibeZone.Modules.Media.Infrastructure;

/// <summary>
/// Runs <see cref="MediaOptions.MaxConcurrentJobs"/> claim loops. Each claimed job gets a heartbeat that
/// extends its lease and watches for cancellation by the API; on cancellation FFmpeg is killed.
/// </summary>
internal sealed partial class MediaWorker(IServiceScopeFactory scopes, IOptions<MediaOptions> options, ILogger<MediaWorker> logger) : BackgroundService
{
    /// <summary>Lease owner id (fits upload_jobs.locked_by, 128 chars).</summary>
    private readonly string _workerId = Truncate($"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}", 128);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger, _workerId, options.Value.MaxConcurrentJobs);
        return Task.WhenAll(Enumerable.Range(0, options.Value.MaxConcurrentJobs).Select(_ => LoopAsync(stoppingToken)));
    }

    private async Task LoopAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            Heartbeat(settings);
            Guid? jobId = null;
            try
            {
                using (var scope = scopes.CreateScope())
                {
                    jobId = await scope.ServiceProvider.GetRequiredService<IMediaRepository>().ClaimNextJobAsync(_workerId, settings.LeaseDuration, stoppingToken);
                }

                if (jobId is null)
                {
                    await Task.Delay(settings.PollInterval, stoppingToken);
                    continue;
                }

                await RunJobAsync(jobId.Value, settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let one bad job (or a DB blip) kill the loop; the lease makes the job retryable.
                LogLoopError(logger, ex, jobId);
                await Task.Delay(settings.PollInterval, stoppingToken);
            }
        }
    }

    private async Task RunJobAsync(Guid jobId, MediaOptions settings, CancellationToken stoppingToken)
    {
        using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var rowGate = new JobRowGate();
        var cancelledByUser = false;
        var heartbeat = Task.Run(async () =>
        {
            try
            {
                while (!jobCts.IsCancellationRequested)
                {
                    await Task.Delay(settings.LeaseDuration / 3, jobCts.Token);
                    using var hold = await rowGate.EnterAsync(jobCts.Token);
                    using var scope = scopes.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IMediaRepository>();
                    if (await repository.ReadStatusAsync(jobId, jobCts.Token) == UploadJobStatus.Cancelled)
                    {
                        cancelledByUser = true;
                        await jobCts.CancelAsync();
                        return;
                    }

                    await repository.ExtendLeaseAsync(jobId, _workerId, settings.LeaseDuration, jobCts.Token);
                    Heartbeat(settings);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, CancellationToken.None);

        try
        {
            using var scope = scopes.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<MediaProcessor>();
            var outcome = await processor.ProcessAsync(jobId, () => cancelledByUser, rowGate, jobCts.Token);
            LogOutcome(logger, jobId, outcome);
        }
        finally
        {
            await jobCts.CancelAsync();
            await heartbeat;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static void Heartbeat(MediaOptions settings)
    {
        if (settings.HeartbeatFile is { Length: > 0 } path)
        {
            File.WriteAllText(path, DateTimeOffset.UtcNow.ToString("O"));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Media worker {WorkerId} started with {Concurrency} slot(s)")]
    private static partial void LogStarted(ILogger logger, string workerId, int concurrency);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} finished: {Outcome}")]
    private static partial void LogOutcome(ILogger logger, Guid jobId, ProcessingOutcome outcome);

    [LoggerMessage(Level = LogLevel.Error, Message = "Media worker loop error (job {JobId})")]
    private static partial void LogLoopError(ILogger logger, Exception ex, Guid? jobId);
}
