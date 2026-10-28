using System.Net;
using VibeZone.IntegrationTests.Infrastructure;

namespace VibeZone.IntegrationTests.Social;

public sealed class SocialTests : IAsyncDisposable
{
    private readonly ApiFactory _api;
    private readonly TestContent _content;

    public SocialTests(PostgresFixture postgres, MailpitFixture mail)
    {
        _api = new ApiFactory(postgres.ConnectionString, TestContent.RelaxedRateLimits, mail);
        _content = new TestContent(_api, postgres.ConnectionString, mail);
    }

    [Fact]
    public async Task Like_and_follow_update_counts_and_notify_the_artist_once()
    {
        var (artist, _, artistId) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist, "Liked song");
        var (fan, _) = await _content.UserAsync();

        for (var i = 0; i < 2; i++) // idempotent
        {
            using var like = await fan.SendAsync(HttpMethod.Put, $"/api/v1/tracks/{track}/like", null);
            Assert.Equal(HttpStatusCode.OK, like.StatusCode);
            var engagement = await like.DataAsync();
            Assert.Equal(1, engagement.GetProperty("likeCount").GetInt32());
            Assert.True(engagement.GetProperty("liked").GetBoolean());
        }

        for (var i = 0; i < 2; i++)
        {
            using var follow = await fan.SendAsync(HttpMethod.Put, $"/api/v1/artists/{artistId}/follow", null);
            Assert.Equal(HttpStatusCode.OK, follow.StatusCode);
            Assert.Equal(1, (await follow.DataAsync()).GetProperty("followerCount").GetInt32());
        }

        Assert.Contains(track, (await fan.GetDataAsync("/api/v1/me/likes/ids")).EnumerateArray().Select(e => e.GetGuid()));
        Assert.Equal([artistId], (await fan.GetDataAsync("/api/v1/me/following")).Ids());

        await TestContent.EventuallyAsync(async () => (await UnreadAsync(artist)) == 2, "the artist has two unread notifications");
        var notifications = (await artist.GetDataAsync("/api/v1/me/notifications")).EnumerateArray().ToList();
        Assert.Equal(["ArtistFollowed", "TrackLiked"], notifications.Select(n => n.GetProperty("type").GetString()).Order());
        Assert.Equal("Liked song", notifications.Single(n => n.GetProperty("type").GetString() == "TrackLiked").GetProperty("title").GetString());

        // Liking your own track or following yourself never notifies.
        using (var own = await artist.SendAsync(HttpMethod.Put, $"/api/v1/tracks/{track}/like", null)) { Assert.Equal(HttpStatusCode.OK, own.StatusCode); }
        using (var self = await artist.SendAsync(HttpMethod.Put, $"/api/v1/artists/{artistId}/follow", null))
        {
            Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
            Assert.Equal("follow_self", await self.ProblemCodeAsync());
        }

        // Unlike / unfollow bring the counts back; the fan still sees the track.
        using (var unlike = await fan.DeleteAsync($"/api/v1/tracks/{track}/like"))
        {
            Assert.Equal(1, (await unlike.DataAsync()).GetProperty("likeCount").GetInt32()); // the artist's own like remains
        }

        using (var unfollow = await fan.DeleteAsync($"/api/v1/artists/{artistId}/follow"))
        {
            Assert.Equal(0, (await unfollow.DataAsync()).GetProperty("followerCount").GetInt32());
        }

        // Mark read: one, then all.
        var first = notifications[0].GetProperty("id").GetGuid();
        using (var read = await artist.PostAsync($"/api/v1/me/notifications/{first}/read")) { Assert.Equal(HttpStatusCode.NoContent, read.StatusCode); }
        Assert.Equal(1, await UnreadAsync(artist));
        using (var readAll = await artist.PostAsync("/api/v1/me/notifications/read-all")) { Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode); }
        Assert.Equal(0, await UnreadAsync(artist));
    }

    [Fact]
    public async Task Redelivered_outbox_message_does_not_notify_twice()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist);
        var (fan, _) = await _content.UserAsync();
        using (await fan.SendAsync(HttpMethod.Put, $"/api/v1/tracks/{track}/like", null)) { }
        await TestContent.EventuallyAsync(async () => (await UnreadAsync(artist)) == 1, "the like notification arrives");

        // At-least-once delivery: the dispatcher picks the same message up again (as after a crash before marking it).
        const string Processed = "SELECT count(*) FROM infra.outbox_messages WHERE type = 'social.track_liked' AND payload::text LIKE @t AND processed_at IS NOT NULL";
        await _content.ExecuteAsync(
            "UPDATE infra.outbox_messages SET processed_at = NULL, next_attempt_at = now() WHERE type = 'social.track_liked' AND payload::text LIKE @t",
            ("t", $"%{track}%"));
        await TestContent.EventuallyAsync(async () => await _content.ScalarAsync(Processed, ("t", $"%{track}%")) == 1, "the message is processed again");

        Assert.Single((await artist.GetDataAsync("/api/v1/me/notifications")).EnumerateArray());
        Assert.Equal(1, await _content.ScalarAsync("SELECT sum(likes) FROM analytics.track_daily_stats WHERE track_id = @t", ("t", track)));
    }

    [Fact]
    public async Task Notifications_belong_to_their_recipient_only()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist);
        var (fan, _) = await _content.UserAsync();
        using (await fan.SendAsync(HttpMethod.Put, $"/api/v1/tracks/{track}/like", null)) { }
        await TestContent.EventuallyAsync(async () => (await UnreadAsync(artist)) == 1, "the like notification arrives");
        var notification = (await artist.GetDataAsync("/api/v1/me/notifications"))[0].GetProperty("id").GetGuid();

        using var stranger = await fan.PostAsync($"/api/v1/me/notifications/{notification}/read");

        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Empty((await fan.GetDataAsync("/api/v1/me/notifications")).EnumerateArray());
        Assert.Equal(1, await UnreadAsync(artist));

        using var anonymous = _api.CreateApiClient();
        using var list = await anonymous.GetAsync("/api/v1/me/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task Hidden_tracks_cannot_be_liked_or_commented_and_look_missing()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var privateTrack = await _content.ReadyTrackAsync(artist, visibility: "Private");
        var draft = await TestContent.DraftTrackAsync(artist);
        var (fan, _) = await _content.UserAsync();

        foreach (var track in new[] { privateTrack, draft, Guid.NewGuid() })
        {
            using var like = await fan.SendAsync(HttpMethod.Put, $"/api/v1/tracks/{track}/like", null);
            Assert.Equal(HttpStatusCode.NotFound, like.StatusCode);
            using var comment = await fan.PostAsync($"/api/v1/tracks/{track}/comments", new { body = "hi" });
            Assert.Equal(HttpStatusCode.NotFound, comment.StatusCode);
            using var comments = await fan.GetAsync($"/api/v1/tracks/{track}/comments");
            Assert.Equal(HttpStatusCode.NotFound, comments.StatusCode);
            using var engagement = await fan.GetAsync($"/api/v1/tracks/{track}/engagement");
            Assert.Equal(HttpStatusCode.NotFound, engagement.StatusCode);
        }

        using var anonymous = _api.CreateApiClient();
        var publicTrack = await _content.ReadyTrackAsync(artist);
        using var anonymousLike = await anonymous.SendAsync(HttpMethod.Put, $"/api/v1/tracks/{publicTrack}/like", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousLike.StatusCode);
    }

    [Fact]
    public async Task Comments_are_plain_text_with_replies_and_only_author_owner_or_moderator_can_delete()
    {
        var (artist, _, _) = await _content.ArtistAsync();
        var track = await _content.ReadyTrackAsync(artist, "Commented");
        var (author, _) = await _content.UserAsync();
        var (other, _) = await _content.UserAsync();

        var commentId = await PostCommentAsync(author, track, new { body = "  <b>nice</b> drop  ", timestampMs = 42_000 });
        var reply = await PostCommentAsync(other, track, new { body = "agreed", parentId = commentId });

        using var anonymous = _api.CreateApiClient();
        var listed = Assert.Single((await anonymous.GetDataAsync($"/api/v1/tracks/{track}/comments")).EnumerateArray());
        Assert.Equal("<b>nice</b> drop", listed.GetProperty("body").GetString()); // stored as text, rendered escaped
        Assert.Equal(42_000, listed.GetProperty("timestampMs").GetInt32());
        Assert.Equal([reply], listed.GetProperty("replies").Ids());

        using (var invalid = await author.PostAsync($"/api/v1/tracks/{track}/comments", new { body = "   " }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("comment_body_invalid", await invalid.ProblemCodeAsync());
        }

        // Someone else can neither edit nor delete it (404, no existence leak).
        using (var edit = await other.PatchAsync($"/api/v1/comments/{commentId}", new { body = "mine now" })) { Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode); }
        using (var delete = await other.DeleteAsync($"/api/v1/comments/{commentId}")) { Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode); }

        // The track owner can't edit it either, but can delete it.
        using (var edit = await artist.PatchAsync($"/api/v1/comments/{commentId}", new { body = "owner edit" })) { Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode); }
        using (var edit = await author.PatchAsync($"/api/v1/comments/{commentId}", new { body = "edited" }))
        {
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
            Assert.Equal("edited", (await edit.DataAsync()).GetProperty("body").GetString());
        }

        using (var delete = await artist.DeleteAsync($"/api/v1/comments/{commentId}")) { Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode); }
        Assert.Empty((await anonymous.GetDataAsync($"/api/v1/tracks/{track}/comments")).EnumerateArray());

        // A moderator can delete anyone's comment.
        var second = await PostCommentAsync(author, track, new { body = "again" });
        var (moderator, _) = await _content.StaffAsync("Moderator");
        using (var delete = await moderator.DeleteAsync($"/api/v1/comments/{second}")) { Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode); }

        // The artist was notified about the top-level comments; the author about the reply.
        // A reply notifies both (one event → two notifications).
        await TestContent.EventuallyAsync(async () => (await UnreadAsync(author)) == 1, "the author hears about the reply");
        var replied = (await author.GetDataAsync("/api/v1/me/notifications"))[0];
        Assert.Equal("CommentReplied", replied.GetProperty("type").GetString());
        Assert.Equal("Commented", replied.GetProperty("title").GetString());
        await TestContent.EventuallyAsync(async () => (await UnreadAsync(artist)) == 3, "the artist hears about both comments and the reply");
    }

    private static async Task<Guid> PostCommentAsync(ApiClient client, Guid track, object body)
    {
        using var response = await client.PostAsync($"/api/v1/tracks/{track}/comments", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.DataAsync()).GetProperty("id").GetGuid();
    }

    private static async Task<int> UnreadAsync(ApiClient client) =>
        (await client.GetDataAsync("/api/v1/me/notifications/unread-count")).GetProperty("count").GetInt32();

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();
}
