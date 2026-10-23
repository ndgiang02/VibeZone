namespace VibeZone.SharedKernel;

/// <summary>
/// A versioned fact another module may react to (ADR-0004). Declared in the producing module's
/// <c>Contracts</c>; <see cref="EventType"/> is stable (<c>module.snake_case</c>) and <see cref="EventVersion"/> bumps
/// on any breaking payload change. Payloads carry ids and display snapshots only — never secrets or emails.
/// </summary>
public interface IIntegrationEvent
{
    static abstract string EventType { get; }

    static abstract int EventVersion { get; }
}

/// <summary>Transactional outbox: the event is saved by the caller's <c>SaveChanges</c>, together with the state change.</summary>
public interface IOutbox
{
    void Enqueue<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent;
}

/// <summary>
/// Handles one event type. Handlers only stage changes in the unit of work; the dispatcher commits them
/// together with a per-handler "consumed" marker, so each handler's effect happens exactly once even when
/// the message is delivered again. Side effects outside the database (realtime push) go in
/// <see cref="IntegrationEventContext.AfterCommit"/> and are best-effort.
/// </summary>
public interface IIntegrationEventConsumer<TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken);
}

public sealed class IntegrationEventContext(Guid messageId, DateTimeOffset occurredAt)
{
    private readonly List<Func<CancellationToken, Task>> _afterCommit = [];

    /// <summary>Outbox message id: a stable idempotency key for the handler's effects.</summary>
    public Guid MessageId { get; } = messageId;

    public DateTimeOffset OccurredAt { get; } = occurredAt;

    public IReadOnlyList<Func<CancellationToken, Task>> AfterCommitActions => _afterCommit;

    public void AfterCommit(Func<CancellationToken, Task> action) => _afterCommit.Add(action);
}
