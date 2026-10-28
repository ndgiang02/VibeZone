using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VibeZone.Infrastructure.Persistence;
using VibeZone.SharedKernel;

namespace VibeZone.Infrastructure.Outbox;

/// <summary>
/// Claims due outbox messages (<c>FOR UPDATE SKIP LOCKED</c>, so several instances can run), runs each registered
/// handler in its own scope and transaction together with its consumption marker, then marks the message
/// processed. A failure retries later with backoff; after <see cref="MaxAttempts"/> the message stays
/// unprocessed for an operator (dead letter, visible in the admin dashboard).
/// </summary>
internal sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopes,
    IEnumerable<IntegrationEventRegistration> registrations,
    TimeProvider time,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    public const int MaxAttempts = 10;
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    private readonly ILookup<string, IntegrationEventRegistration> _handlers = registrations.ToLookup(r => r.EventType);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ids = await ClaimAsync(stoppingToken);
                var dispatched = 0;
                try
                {
                    for (; dispatched < ids.Count; dispatched++)
                    {
                        await DispatchAsync(ids[dispatched], stoppingToken);
                    }
                }
                finally
                {
                    // Stopping mid-batch: hand the rest (and the interrupted one) back now instead of after the lease,
                    // so another instance picks them up right away. Consumption markers make re-running safe.
                    if (dispatched < ids.Count)
                    {
                        await ReleaseAsync(ids.Skip(dispatched).ToList());
                    }
                }

                if (ids.Count < BatchSize)
                {
                    await Task.Delay(PollInterval, time, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A database blip must not stop dispatching; leases make claimed messages retryable.
                LogLoopError(logger, ex);
                await Task.Delay(PollInterval * 5, time, stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<Guid>> ClaimAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = time.GetUtcNow();
        var until = now + Lease;
        return await db.Database.SqlQuery<Guid>($"""
            UPDATE infra.outbox_messages SET next_attempt_at = {until}
            WHERE id IN (
                SELECT id FROM infra.outbox_messages
                WHERE processed_at IS NULL AND attempts < {MaxAttempts} AND next_attempt_at <= {now}
                ORDER BY next_attempt_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED)
            RETURNING id AS "Value"
            """).ToListAsync(cancellationToken);
    }

    private async Task ReleaseAsync(List<Guid> ids)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var scope = scopes.CreateScope();
            var now = time.GetUtcNow();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboxMessage>()
                .Where(m => ids.Contains(m.Id) && m.ProcessedAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.NextAttemptAt, now), timeout.Token);
        }
        catch (Exception ex)
        {
            // Not fatal: the lease expires and the messages are claimed again.
            LogReleaseFailed(logger, ex, ids.Count);
        }
    }

    private async Task DispatchAsync(Guid messageId, CancellationToken cancellationToken)
    {
        OutboxMessage? message;
        using (var scope = scopes.CreateScope())
        {
            message = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboxMessage>().AsNoTracking()
                .SingleOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        }

        if (message is null)
        {
            return;
        }

        foreach (var registration in _handlers[message.Type])
        {
            try
            {
                await RunHandlerAsync(message, registration, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LogHandlerFailed(logger, ex, message.Type, messageId, registration.HandlerName, message.Attempts + 1);
                await RecordFailureAsync(message, ex, cancellationToken);
                return;
            }
        }

        await UpdateAsync(messageId, u => u.SetProperty(m => m.ProcessedAt, time.GetUtcNow()), cancellationToken);
    }

    private async Task RunHandlerAsync(OutboxMessage message, IntegrationEventRegistration registration, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Set<OutboxConsumption>().AnyAsync(c => c.MessageId == message.Id && c.Handler == registration.HandlerName, cancellationToken))
        {
            return; // applied on an earlier delivery
        }

        var context = new IntegrationEventContext(message.Id, message.OccurredAt);
        db.Set<OutboxConsumption>().Add(new OutboxConsumption(message.Id, registration.HandlerName, time.GetUtcNow()));
        await registration.Invoke(scope.ServiceProvider, registration.Deserialize(message.Payload), context, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var action in context.AfterCommitActions)
        {
            try
            {
                await action(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogAfterCommitFailed(logger, ex, message.Type, message.Id);
            }
        }
    }

    private Task RecordFailureAsync(OutboxMessage message, Exception ex, CancellationToken cancellationToken)
    {
        var attempts = message.Attempts + 1;
        // 10 s, 20 s, 40 s … capped at one hour.
        var delay = TimeSpan.FromSeconds(Math.Min(3600, 10 * Math.Pow(2, attempts - 1)));
        var error = ex.GetType().Name is { Length: > OutboxMessage.MaxErrorLength } name ? name[..OutboxMessage.MaxErrorLength] : ex.GetType().Name;
        return UpdateAsync(
            message.Id,
            u => u.SetProperty(m => m.Attempts, attempts).SetProperty(m => m.LastError, error).SetProperty(m => m.NextAttemptAt, time.GetUtcNow() + delay),
            CancellationToken.None);
    }

    private async Task UpdateAsync(
        Guid messageId,
        Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<OutboxMessage>> setters,
        CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboxMessage>()
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(setters, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox dispatcher loop error")]
    private static partial void LogLoopError(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox handler {Handler} failed for {EventType} {MessageId} (attempt {Attempt})")]
    private static partial void LogHandlerFailed(ILogger logger, Exception ex, string eventType, Guid messageId, string handler, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not release {Count} claimed outbox messages; they are retried when the lease expires")]
    private static partial void LogReleaseFailed(ILogger logger, Exception ex, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "After-commit action failed for {EventType} {MessageId}")]
    private static partial void LogAfterCommitFailed(ILogger logger, Exception ex, string eventType, Guid messageId);
}
