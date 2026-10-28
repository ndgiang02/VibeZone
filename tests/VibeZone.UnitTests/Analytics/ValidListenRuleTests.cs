using VibeZone.Modules.Analytics.Domain;

namespace VibeZone.UnitTests.Analytics;

public sealed class ValidListenRuleTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, 30_000)] // unknown duration
    [InlineData(240_000, 30_000)]
    [InlineData(60_000, 30_000)] // exactly 60 s is not "shorter than 60 s"
    [InlineData(59_998, 29_999)]
    [InlineData(10_000, 5_000)]
    [InlineData(1, 1)] // never zero
    public void Threshold_is_30_seconds_or_half_of_a_short_track(int? durationMs, int expected) =>
        Assert.Equal(expected, ValidListenRule.ThresholdMs(durationMs));

    [Fact]
    public void Listener_is_the_account_else_a_hash_of_the_client_id_else_the_session()
    {
        var user = Guid.NewGuid();
        var client = Guid.NewGuid();
        var session = Guid.NewGuid();

        Assert.Equal($"u:{user:N}", ValidListenRule.ListenerKey(user, client, session));
        var anonymous = ValidListenRule.ListenerKey(null, client, session);
        Assert.StartsWith("a:", anonymous, StringComparison.Ordinal);
        Assert.DoesNotContain(client.ToString("N"), anonymous, StringComparison.Ordinal); // raw id never stored
        Assert.Equal(anonymous, ValidListenRule.ListenerKey(null, client, Guid.NewGuid())); // stable across sessions
        Assert.Equal($"s:{session:N}", ValidListenRule.ListenerKey(null, null, session));
        Assert.All(new[] { ValidListenRule.ListenerKey(user, null, session), anonymous }, k => Assert.True(k.Length <= PlaySession.MaxListenerKeyLength));
    }

    [Fact]
    public void Reported_listening_is_capped_by_elapsed_time_plus_slack()
    {
        var session = PlaySession.Start(Guid.NewGuid(), "u:x", null, Guid.NewGuid(), Start);

        session.RecordProgress(180_000, Start.AddSeconds(5));

        Assert.Equal(15_000, session.ListenedMs);
        Assert.False(session.ReachedThreshold(240_000));

        session.RecordProgress(31_000, Start.AddSeconds(25));
        Assert.Equal(31_000, session.ListenedMs);
        Assert.True(session.ReachedThreshold(240_000));
    }

    [Fact]
    public void Listening_time_never_goes_backwards_or_negative()
    {
        var session = PlaySession.Start(Guid.NewGuid(), "u:x", null, Guid.NewGuid(), Start);
        session.RecordProgress(20_000, Start.AddMinutes(1));

        session.RecordProgress(5_000, Start.AddMinutes(2));
        session.RecordProgress(-1, Start.AddMinutes(3));

        Assert.Equal(20_000, session.ListenedMs);
    }

    [Fact]
    public void A_session_belongs_to_one_listener_and_one_track()
    {
        var track = Guid.NewGuid();
        var session = PlaySession.Start(Guid.NewGuid(), "u:a", null, track, Start);

        Assert.True(session.BelongsTo("u:a", track));
        Assert.False(session.BelongsTo("u:b", track));
        Assert.False(session.BelongsTo("u:a", Guid.NewGuid()));
    }
}
