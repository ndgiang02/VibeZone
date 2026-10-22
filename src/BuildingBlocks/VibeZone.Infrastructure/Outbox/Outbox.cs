using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using VibeZone.Infrastructure.Persistence;
using VibeZone.SharedKernel;

namespace VibeZone.Infrastructure.Outbox;

/// <summary>An integration event waiting for (or done with) dispatch (ADR-0004, ADR-0012).</summary>
public sealed class OutboxMessage
{
    public const int MaxTypeLength = 100;
    public const int MaxErrorLength = 200;

    private OutboxMessage() { }

    public static OutboxMessage Create(string type, int version, string payload, DateTimeOffset now) => new()
    {
        Id = EntityId.New(),
        Type = type,
        Version = version,
        Payload = payload,
        OccurredAt = now,
        NextAttemptAt = now,
    };

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public int Version { get; private set; }

    /// <summary>JSON (camelCase) of the event record.</summary>
    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>Due time; also the claim lease while a dispatcher works on it.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>Exception type of the last failure (no messages: they may contain data).</summary>
    public string? LastError { get; private set; }
}

/// <summary>Marks that one handler has applied one message, committed with the handler's own changes.</summary>
public sealed class OutboxConsumption
{
    private OutboxConsumption() { }

    public OutboxConsumption(Guid messageId, string handler, DateTimeOffset consumedAt)
    {
        MessageId = messageId;
        Handler = handler;
        ConsumedAt = consumedAt;
    }

    public Guid MessageId { get; private set; }

    public string Handler { get; private set; } = string.Empty;

    public DateTimeOffset ConsumedAt { get; private set; }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages", AppDbContext.InfraSchema);
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).ValueGeneratedNever();
        b.Property(m => m.Type).HasMaxLength(OutboxMessage.MaxTypeLength).IsRequired();
        b.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(m => m.LastError).HasMaxLength(OutboxMessage.MaxErrorLength);
        b.HasIndex(m => m.NextAttemptAt).HasDatabaseName("ix_outbox_messages_due").HasFilter("processed_at IS NULL");
    }
}

internal sealed class OutboxConsumptionConfiguration : IEntityTypeConfiguration<OutboxConsumption>
{
    public void Configure(EntityTypeBuilder<OutboxConsumption> b)
    {
        b.ToTable("outbox_consumptions", AppDbContext.InfraSchema);
        b.HasKey(c => new { c.MessageId, c.Handler });
        b.Property(c => c.Handler).HasMaxLength(200);
        b.HasOne<OutboxMessage>().WithMany().HasForeignKey(c => c.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Adds the message to the scoped <see cref="AppDbContext"/>; the caller's SaveChanges commits it atomically.</summary>
internal sealed class EfOutbox(AppDbContext db, TimeProvider time) : IOutbox
{
    public void Enqueue<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent =>
        db.Set<OutboxMessage>().Add(OutboxMessage.Create(
            TEvent.EventType,
            TEvent.EventVersion,
            JsonSerializer.Serialize(integrationEvent, OutboxJson.Options),
            time.GetUtcNow()));
}

internal static class OutboxJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>One handler for one event type, invoked without reflection.</summary>
public sealed record IntegrationEventRegistration(
    string EventType,
    string HandlerName,
    Func<string, object> Deserialize,
    Func<IServiceProvider, object, IntegrationEventContext, CancellationToken, Task> Invoke);

public static class OutboxServiceCollectionExtensions
{
    /// <summary>Registers a handler; the dispatcher runs it for every <typeparamref name="TEvent"/> in the outbox.</summary>
    public static IServiceCollection AddIntegrationEventConsumer<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventConsumer<TEvent>
    {
        services.AddScoped<THandler>();
        services.AddSingleton(new IntegrationEventRegistration(
            TEvent.EventType,
            typeof(THandler).FullName ?? typeof(THandler).Name,
            json => JsonSerializer.Deserialize<TEvent>(json, OutboxJson.Options) ?? throw new JsonException($"Empty {TEvent.EventType} payload."),
            (sp, e, context, ct) => sp.GetRequiredService<THandler>().HandleAsync((TEvent)e, context, ct)));
        return services;
    }

    /// <summary>The <see cref="IOutbox"/> every host needs to write events (the API, and the worker's track updates).</summary>
    internal static IServiceCollection AddOutboxWriter(this IServiceCollection services) =>
        services.AddScoped<IOutbox, EfOutbox>();

    /// <summary>Dispatches outbox messages to the registered handlers (API host: handlers push realtime notifications).</summary>
    public static IServiceCollection AddOutboxDispatcher(this IServiceCollection services)
    {
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}
