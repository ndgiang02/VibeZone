"use client";

import { Clock3, CornerDownRight, MessageSquare, Pencil, Trash2 } from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { useId, useState, type FormEvent } from "react";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { useMe } from "@/features/auth/api";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { ReportButton } from "@/features/moderation/report-button";
import type { Track } from "@/features/tracks/api";
import { type Comment, useComments, useDeleteComment, useEditComment, usePostComment } from "./api";

const MAX_LENGTH = 1000;

/**
 * Timed comments: a comment can be pinned to the moment that was playing when it was written; the
 * timestamp chip plays the track from there. Replies are one level deep. Text is rendered as text.
 */
export function CommentsSection({ track }: { track: Track }) {
  const t = useTranslations("comments");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const me = useMe();
  const comments = useComments(track.id);
  const headingId = useId();
  const items = comments.data?.pages.flatMap((p) => p.items) ?? [];
  const total = comments.data?.pages[0]?.meta.total ?? 0;

  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col gap-4 rounded-2xl border bg-surface-card p-5"
    >
      <h2 id={headingId} className="flex items-center gap-2 font-semibold">
        <MessageSquare className="size-4 text-subtle-foreground" aria-hidden="true" />
        {t("title")}
        {comments.isSuccess ? (
          <span className="text-sm font-normal text-subtle-foreground">({total})</span>
        ) : null}
      </h2>

      {me.data ? (
        track.playable ? (
          <CommentForm track={track} />
        ) : null
      ) : (
        <p className="text-sm text-muted-foreground">
          <Link href="/login" className="font-medium text-primary hover:underline">
            {t("signIn")}
          </Link>
        </p>
      )}

      {comments.isPending ? (
        <div className="flex flex-col gap-3" aria-busy="true" aria-label={tCommon("loading")}>
          <Skeleton className="h-14" />
          <Skeleton className="h-14" />
        </div>
      ) : comments.isError ? (
        <ErrorState
          title={tErrors(errorCode(comments.error))}
          onRetry={() => void comments.refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : items.length === 0 ? (
        <EmptyState
          icon={MessageSquare}
          title={t("emptyTitle")}
          description={t("emptyBody")}
          className="py-8"
        />
      ) : (
        <ul className="flex flex-col gap-4">
          {items.map((comment) => (
            <li key={comment.id}>
              <CommentItem comment={comment} track={track} />
            </li>
          ))}
        </ul>
      )}

      {comments.hasNextPage ? (
        <Button
          variant="outline"
          className="self-center"
          disabled={comments.isFetchingNextPage}
          onClick={() => void comments.fetchNextPage()}
        >
          {comments.isFetchingNextPage ? tCommon("loading") : tCommon("showMore")}
        </Button>
      ) : null}
    </section>
  );
}

function CommentForm({ track, parent, onDone }: { track: Track; parent?: Comment; onDone?: () => void }) {
  const t = useTranslations("comments");
  const tErrors = useTranslations("errors");
  const post = usePostComment(track.id);
  const [body, setBody] = useState("");
  const [pin, setPin] = useState(true);
  const fieldId = useId();
  const isCurrent = usePlayerStore((s) => s.queue[s.index]?.id === track.id);
  const currentTime = usePlayerStore((s) => s.currentTime);
  // Only top-level comments sit on the timeline, and only if this track is the one in the player.
  const canPin = !parent && isCurrent && currentTime > 0;

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const text = body.trim();
    if (!text) return;
    post.mutate(
      {
        body: text,
        parentId: parent?.parentId ?? parent?.id ?? null,
        timestampMs: canPin && pin ? Math.floor(currentTime * 1000) : null,
      },
      {
        onSuccess: () => {
          setBody("");
          onDone?.();
        },
      },
    );
  };

  return (
    <form onSubmit={submit} className="flex flex-col gap-2">
      <label htmlFor={fieldId} className="sr-only">
        {parent ? t("replyLabel", { name: parent.author.displayName }) : t("label")}
      </label>
      <textarea
        id={fieldId}
        value={body}
        onChange={(event) => setBody(event.target.value)}
        maxLength={MAX_LENGTH}
        rows={parent ? 2 : 3}
        placeholder={parent ? t("replyPlaceholder") : t("placeholder")}
        className="w-full resize-y rounded-xl border bg-background px-3 py-2 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
      />
      <div className="flex flex-wrap items-center gap-3">
        {canPin ? (
          <label className="flex items-center gap-2 text-xs text-muted-foreground">
            <input type="checkbox" checked={pin} onChange={(event) => setPin(event.target.checked)} />
            {t("pinAt", { time: formatTime(currentTime) })}
          </label>
        ) : null}
        <span className="ml-auto text-xs text-subtle-foreground tabular-nums">
          {body.length}/{MAX_LENGTH}
        </span>
        {onDone ? (
          <Button type="button" variant="ghost" onClick={onDone}>
            {t("cancel")}
          </Button>
        ) : null}
        <Button type="submit" disabled={post.isPending || body.trim().length === 0}>
          {parent ? t("reply") : t("post")}
        </Button>
      </div>
      {post.isError ? (
        <p role="alert" className="text-sm text-destructive">
          {tErrors(errorCode(post.error))}
        </p>
      ) : null}
    </form>
  );
}

function CommentItem({
  comment,
  track,
  isReply = false,
}: {
  comment: Comment;
  track: Track;
  isReply?: boolean;
}) {
  const t = useTranslations("comments");
  const tErrors = useTranslations("errors");
  const format = useFormatter();
  const me = useMe();
  const [replying, setReplying] = useState(false);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(comment.body);
  const edit = useEditComment(track.id);
  const remove = useDeleteComment(track.id);
  const timestamp = comment.timestampMs == null ? null : Number(comment.timestampMs) / 1000;

  const playAt = (seconds: number) => {
    const store = usePlayerStore.getState();
    if (store.queue[store.index]?.id !== track.id) store.playTrack(toPlayerTrack(track));
    else if (store.status !== "playing") store.togglePlay();
    store.seek(seconds);
  };

  return (
    <article className={cn("flex gap-3", isReply && "ml-6")}>
      {isReply ? (
        <CornerDownRight className="mt-2 size-4 shrink-0 text-subtle-foreground" aria-hidden="true" />
      ) : null}
      <ArtistAvatar
        id={comment.author.id}
        name={comment.author.displayName || comment.author.handle}
        src={comment.author.avatarUrl}
        className="size-8 shrink-0 text-[0.625rem]"
      />
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <header className="flex flex-wrap items-baseline gap-x-2 text-sm">
          <span className="font-semibold">{comment.author.displayName || comment.author.handle}</span>
          <time dateTime={comment.createdAt} className="text-xs text-subtle-foreground">
            {format.relativeTime(new Date(comment.createdAt))}
          </time>
          {comment.editedAt ? <span className="text-xs text-subtle-foreground">{t("edited")}</span> : null}
          {timestamp != null ? (
            <button
              type="button"
              disabled={!track.playable}
              onClick={() => playAt(timestamp)}
              aria-label={t("playAt", { time: formatTime(timestamp) })}
              className="inline-flex items-center gap-1 rounded-full bg-primary-subtle px-2 py-0.5 text-xs font-medium text-primary tabular-nums hover:bg-primary/20"
            >
              <Clock3 className="size-3" aria-hidden="true" />
              {formatTime(timestamp)}
            </button>
          ) : null}
        </header>

        {editing ? (
          <form
            className="flex flex-col gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              edit.mutate(
                { commentId: comment.id, body: draft.trim() },
                { onSuccess: () => setEditing(false) },
              );
            }}
          >
            <textarea
              aria-label={t("editLabel")}
              value={draft}
              maxLength={MAX_LENGTH}
              onChange={(event) => setDraft(event.target.value)}
              rows={2}
              className="w-full resize-y rounded-xl border bg-background px-3 py-2 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
            />
            <div className="flex gap-2">
              <Button type="submit" size="sm" disabled={edit.isPending || draft.trim().length === 0}>
                {t("save")}
              </Button>
              <Button type="button" size="sm" variant="ghost" onClick={() => setEditing(false)}>
                {t("cancel")}
              </Button>
            </div>
          </form>
        ) : (
          // Plain text, never HTML.
          <p className="text-sm break-words whitespace-pre-line text-muted-foreground">{comment.body}</p>
        )}

        <div className="flex flex-wrap items-center gap-1 text-xs">
          {me.data && !isReply && track.playable ? (
            <button
              type="button"
              onClick={() => setReplying((v) => !v)}
              className="rounded-full px-2 py-1 font-medium text-muted-foreground hover:bg-surface-hover hover:text-foreground"
            >
              {t("reply")}
            </button>
          ) : null}
          {comment.isAuthor && !editing ? (
            <button
              type="button"
              onClick={() => {
                setDraft(comment.body);
                setEditing(true);
              }}
              aria-label={t("edit")}
              className="flex size-7 items-center justify-center rounded-full text-muted-foreground hover:bg-surface-hover hover:text-foreground"
            >
              <Pencil className="size-3.5" aria-hidden="true" />
            </button>
          ) : null}
          {comment.canDelete ? (
            <button
              type="button"
              disabled={remove.isPending}
              onClick={() => {
                if (window.confirm(t("deleteConfirm"))) remove.mutate(comment.id);
              }}
              aria-label={t("delete")}
              className="flex size-7 items-center justify-center rounded-full text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            >
              <Trash2 className="size-3.5" aria-hidden="true" />
            </button>
          ) : null}
          {me.data && !comment.isAuthor ? (
            <ReportButton subjectType="Comment" subjectId={comment.id} compact />
          ) : null}
        </div>
        {edit.isError || remove.isError ? (
          <p role="alert" className="text-sm text-destructive">
            {tErrors(errorCode(edit.error ?? remove.error))}
          </p>
        ) : null}

        {replying ? <CommentForm track={track} parent={comment} onDone={() => setReplying(false)} /> : null}

        {comment.replies.length > 0 ? (
          <ul className="mt-2 flex flex-col gap-3">
            {comment.replies.map((reply) => (
              <li key={reply.id}>
                <CommentItem comment={reply} track={track} isReply />
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </article>
  );
}
