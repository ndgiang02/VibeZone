using VibeZone.Modules.Catalog.Contracts;
using VibeZone.Modules.Identity.Contracts;
using VibeZone.Modules.Social.Contracts;
using VibeZone.Modules.Social.Domain;
using VibeZone.SharedKernel;

namespace VibeZone.Modules.Social.Application;

/// <summary>Timed comments on tracks. Reading needs the track to be visible; posting needs it to be playable.</summary>
public sealed class CommentService(ISocialRepository repository, ITrackCatalog tracks, IUserDirectory users, IOutbox outbox, TimeProvider time)
{
    private static readonly DomainError TrackNotFound = DomainError.NotFound("track_not_found", "Track not found.");
    private static readonly DomainError CommentNotFound = DomainError.NotFound("comment_not_found", "Comment not found.");

    public async Task<Result<Paged<CommentDto>>> ListAsync(Guid trackId, Guid? viewerUserId, bool viewerIsModerator, PageRequest page, CancellationToken cancellationToken)
    {
        if (!await tracks.CanViewAsync(trackId, viewerUserId, viewerIsModerator, cancellationToken) ||
            (await tracks.GetInfoAsync([trackId], cancellationToken)).GetValueOrDefault(trackId) is not { } track)
        {
            return TrackNotFound;
        }

        var top = await repository.PageTopLevelCommentsAsync(trackId, page, cancellationToken);
        var replies = await repository.ListRepliesAsync(top.Items.Select(c => c.Id).ToList(), cancellationToken);
        var authors = await users.GetAsync(top.Items.Concat(replies).Select(c => c.AuthorUserId).Distinct().ToList(), cancellationToken);
        var byParent = replies.ToLookup(r => r.ParentId);
        var items = top.Items
            .Select(c => ToDto(c, track, authors, viewerUserId, viewerIsModerator,
                byParent[c.Id].Select(r => ToDto(r, track, authors, viewerUserId, viewerIsModerator, [])).ToList()))
            .ToList();
        return new Paged<CommentDto>(items, top.Page, top.PageSize, top.Total);
    }

    public async Task<Result<CommentDto>> PostAsync(Guid userId, Guid trackId, PostCommentRequest request, CancellationToken cancellationToken)
    {
        if (!await tracks.CanStreamAsync(trackId, userId, false, cancellationToken) ||
            (await tracks.GetInfoAsync([trackId], cancellationToken)).GetValueOrDefault(trackId) is not { } track)
        {
            return TrackNotFound;
        }

        Comment? parent = null;
        if (request.ParentId is { } parentId)
        {
            parent = await repository.FindCommentAsync(parentId, cancellationToken);
            if (parent is null || parent.IsDeleted || parent.TrackId != trackId)
            {
                return CommentNotFound;
            }
        }

        var body = Comment.NormalizeBody(request.Body);
        if (Comment.Validate(body, parent is null ? request.TimestampMs : null, track.DurationMs) is { } invalid)
        {
            return invalid;
        }

        var now = time.GetUtcNow();
        var comment = Comment.Create(trackId, userId, parent, body, request.TimestampMs, now);
        repository.Add(comment);
        outbox.Enqueue(new CommentPosted(comment.Id, trackId, track.Title, track.OwnerUserId, userId, parent?.AuthorUserId, comment.Excerpt, now));
        await repository.SaveChangesAsync(cancellationToken);

        var authors = await users.GetAsync([userId], cancellationToken);
        return ToDto(comment, track, authors, userId, false, []);
    }

    public async Task<Result<CommentDto>> EditAsync(Guid userId, Guid commentId, EditCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await repository.FindCommentAsync(commentId, cancellationToken);
        if (comment is null || comment.IsDeleted || comment.AuthorUserId != userId ||
            (await tracks.GetInfoAsync([comment.TrackId], cancellationToken)).GetValueOrDefault(comment.TrackId) is not { } track)
        {
            return CommentNotFound;
        }

        var body = Comment.NormalizeBody(request.Body);
        if (Comment.Validate(body, null, null) is { } invalid)
        {
            return invalid;
        }

        comment.Edit(body, time.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        var authors = await users.GetAsync([userId], cancellationToken);
        return ToDto(comment, track, authors, userId, false, []);
    }

    /// <summary>The author, the track's owner or a moderator may delete. Others get 404 (no existence leak).</summary>
    public async Task<Result> DeleteAsync(Guid userId, bool isModerator, Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await repository.FindCommentAsync(commentId, cancellationToken);
        if (comment is null || comment.IsDeleted)
        {
            return Result.Failure(CommentNotFound);
        }

        var ownsTrack = (await tracks.GetInfoAsync([comment.TrackId], cancellationToken)).GetValueOrDefault(comment.TrackId)?.OwnerUserId == userId;
        if (comment.AuthorUserId != userId && !ownsTrack && !isModerator)
        {
            return Result.Failure(CommentNotFound);
        }

        comment.Delete(time.GetUtcNow(), byModeration: false);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static CommentDto ToDto(
        Comment c, TrackInfo track, IReadOnlyDictionary<Guid, UserSummary> authors, Guid? viewerUserId, bool viewerIsModerator, IReadOnlyList<CommentDto> replies)
    {
        var author = authors.TryGetValue(c.AuthorUserId, out var a)
            ? new CommentAuthorDto(a.Id, a.DisplayName, a.Handle, a.AvatarUrl)
            : new CommentAuthorDto(c.AuthorUserId, string.Empty, string.Empty, null);
        var isAuthor = viewerUserId == c.AuthorUserId;
        return new CommentDto(c.Id, c.TrackId, c.ParentId, c.Body, c.TimestampMs, c.CreatedAt, c.EditedAt, author, isAuthor,
            isAuthor || viewerIsModerator || (viewerUserId is not null && viewerUserId == track.OwnerUserId), replies);
    }
}
