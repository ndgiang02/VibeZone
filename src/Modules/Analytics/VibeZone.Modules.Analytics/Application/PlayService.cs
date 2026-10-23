using VibeZone.Modules.Analytics.Domain;
using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Social.Contracts;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Analytics.Application;

/// <param name="Id">Client-generated idempotency key (UUID). Re-sending an event is a no-op.</param>
/// <param name="SessionId">Client-generated id of this playback (one per track start).</param>
/// <param name="Type"><c>started</c>, <c>progress</c> or <c>completed</c>.</param>
/// <param name="ListenedMs">Playback time accumulated in this session so far (seeks don't count).</param>
public sealed record PlayEventRequest(Guid Id, Guid SessionId, Guid TrackId, string? Type, int PositionMs, int ListenedMs, DateTimeOffset? ClientTimestamp);

/// <param name="ClientId">Anonymous listeners: a random id the browser keeps, so their valid listens are de-duplicated.</param>
public sealed record PlayBatchRequest(Guid? ClientId, IReadOnlyList<PlayEventRequest>? Events);

/// <param name="Accepted">New events stored (duplicates and events for tracks you can't play are skipped).</param>
public sealed record PlayBatchResultDto(int Accepted);

/// <param name="Plays">Playbacks started, all time.</param>
/// <param name="Listens">Valid listens (≥ 30 s, or half of a short track), all time.</param>
public sealed record TrackStatsDto(long Plays, long Listens);

/// <summary>Counter increments applied atomically (upsert) when a batch is saved.</summary>
public sealed record StatIncrement(Guid TrackId, DateOnly Day, int Plays, int ValidListens);

/// <summary>Persistence port for the Analytics module.</summary>
public interface IAnalyticsRepository
{
    Task<IReadOnlySet<Guid>> ExistingEventIdsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken);

    /// <summary>Tracked for update.</summary>
    Task<PlaySession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<bool> HasValidListenSinceAsync(string listenerKey, Guid trackId, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Tracked for update; creates the row (in the unit of work) if missing.</summary>
    Task<TrackDailyStat> GetOrAddDailyStatAsync(Guid trackId, DateOnly day, CancellationToken cancellationToken);

    Task<TrackStatsDto> GetTotalsAsync(Guid trackId, CancellationToken cancellationToken);

    void Add(PlayEvent playEvent);

    void Add(PlaySession session);

    /// <summary>Saves the unit of work and applies the increments in one transaction.</summary>
    Task SaveAsync(IReadOnlyCollection<StatIncrement> increments, CancellationToken cancellationToken);
}

public sealed class PlayService(IAnalyticsRepository repository, ITrackCatalog tracks, TimeProvider time)
{
    public const int MaxBatchSize = 50;

    public async Task<Result<PlayBatchResultDto>> RecordAsync(Guid? userId, PlayBatchRequest request, CancellationToken cancellationToken)
    {
        var events = request.Events ?? [];
        if (events.Count is 0 or > MaxBatchSize)
        {
            return DomainError.Validation("play_batch_invalid", $"Send 1-{MaxBatchSize} events.", "events");
        }

        var parsed = new List<(PlayEventRequest Request, PlayEventType Type)>();
        foreach (var e in events)
        {
            if (e.Id == Guid.Empty || e.SessionId == Guid.Empty || e.PositionMs < 0 || e.ListenedMs < 0 ||
                !Enum.TryParse<PlayEventType>(e.Type, ignoreCase: true, out var type) || !Enum.IsDefined(type))
            {
                return DomainError.Validation("play_event_invalid", "Each event needs ids, a known type and non-negative times.", "events");
            }

            parsed.Add((e, type));
        }

        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var existing = await repository.ExistingEventIdsAsync(parsed.Select(p => p.Request.Id).ToList(), cancellationToken);
        var trackIds = parsed.Select(p => p.Request.TrackId).Distinct().ToList();
        var info = await tracks.GetInfoAsync(trackIds, cancellationToken);
        var playable = new Dictionary<Guid, bool>();
        foreach (var trackId in trackIds)
        {
            playable[trackId] = await tracks.CanStreamAsync(trackId, userId, false, cancellationToken);
        }

        var sessions = new Dictionary<Guid, PlaySession?>();
        var validNow = new HashSet<(string, Guid)>();
        var increments = new Dictionary<Guid, (int Plays, int Valid)>();
        var seen = new HashSet<Guid>();
        var accepted = 0;

        foreach (var (e, type) in parsed)
        {
            if (existing.Contains(e.Id) || !seen.Add(e.Id) || !playable[e.TrackId])
            {
                continue;
            }

            var listener = ValidListenRule.ListenerKey(userId, request.ClientId, e.SessionId);
            if (!sessions.TryGetValue(e.SessionId, out var session))
            {
                session = await repository.FindSessionAsync(e.SessionId, cancellationToken);
                if (session is null)
                {
                    session = PlaySession.Start(e.SessionId, listener, userId, e.TrackId, now);
                    repository.Add(session);
                    Bump(increments, e.TrackId, plays: 1, valid: 0);
                }

                sessions[e.SessionId] = session;
            }

            // A session id belongs to one listener and one track; anything else is ignored, not merged.
            if (session is null || !session.BelongsTo(listener, e.TrackId))
            {
                continue;
            }

            repository.Add(new PlayEvent(e.Id, e.SessionId, userId, e.TrackId, type, e.PositionMs, e.ListenedMs, e.ClientTimestamp, now));
            session.RecordProgress(e.ListenedMs, now);
            accepted++;

            var durationMs = info.TryGetValue(e.TrackId, out var track) ? track.DurationMs : null;
            if (!session.IsValidListen && session.ReachedThreshold(durationMs) && !validNow.Contains((listener, e.TrackId)) &&
                !await repository.HasValidListenSinceAsync(listener, e.TrackId, now - ValidListenRule.DedupeWindow, cancellationToken))
            {
                session.MarkValid(now);
                validNow.Add((listener, e.TrackId));
                Bump(increments, e.TrackId, plays: 0, valid: 1);
            }
        }

        await repository.SaveAsync(increments.Select(i => new StatIncrement(i.Key, today, i.Value.Plays, i.Value.Valid)).ToList(), cancellationToken);
        return new PlayBatchResultDto(accepted);
    }

    public async Task<Result<TrackStatsDto>> GetStatsAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, CancellationToken cancellationToken) =>
        await tracks.CanViewAsync(trackId, viewerUserId, viewerIsModerator, cancellationToken)
            ? await repository.GetTotalsAsync(trackId, cancellationToken)
            : DomainError.NotFound("track_not_found", "Track not found.");

    private static void Bump(Dictionary<Guid, (int Plays, int Valid)> increments, Guid trackId, int plays, int valid)
    {
        var current = increments.GetValueOrDefault(trackId);
        increments[trackId] = (current.Plays + plays, current.Valid + valid);
    }
}

/// <summary>Likes feed trending (consumes Social's event through the outbox).</summary>
public sealed class TrackLikedStatsHandler(IAnalyticsRepository repository) : IIntegrationEventConsumer<TrackLiked>
{
    public async Task HandleAsync(TrackLiked integrationEvent, IntegrationEventContext context, CancellationToken cancellationToken) =>
        (await repository.GetOrAddDailyStatAsync(integrationEvent.TrackId, DateOnly.FromDateTime(integrationEvent.LikedAt.UtcDateTime), cancellationToken)).AddLike();
}
