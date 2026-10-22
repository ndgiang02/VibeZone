import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import { api, ensureOk, unwrap, unwrapPage } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];
export type Engagement = Schemas["TrackEngagementDto"];
export type FollowSummary = Schemas["FollowSummaryDto"];
export type Comment = Schemas["CommentDto"];
export type LikedTrack = Schemas["LikedTrackDto"];
export type HistoryEntry = Schemas["HistoryEntryDto"];
export type FollowedArtist = Schemas["FollowedArtistDto"];

export const socialKeys = {
  all: ["social"] as const,
  engagement: (trackId: string) => ["social", "engagement", trackId] as const,
  likedIds: ["social", "liked-ids"] as const,
  liked: ["social", "liked"] as const,
  history: ["social", "history"] as const,
  following: ["social", "following"] as const,
  follow: (artistId: string) => ["social", "follow", artistId] as const,
  comments: (trackId: string) => ["social", "comments", trackId] as const,
};

const PAGE_SIZE = 20;

/** Like/comment counts and whether the viewer liked the track. */
export function useEngagement(trackId: string, enabled = true) {
  return useQuery({
    queryKey: socialKeys.engagement(trackId),
    queryFn: async ({ signal }) =>
      unwrap(await api.GET("/api/v1/tracks/{trackId}/engagement", { params: { path: { trackId } }, signal })),
    enabled,
  });
}

/** Ids of the tracks the signed-in user liked (hearts in lists); empty set when signed out. */
export function useLikedIds(enabled: boolean) {
  return useQuery({
    queryKey: socialKeys.likedIds,
    queryFn: async ({ signal }) =>
      new Set(unwrap(await api.GET("/api/v1/me/likes/ids", { signal })).map(String)),
    enabled,
    staleTime: 60_000,
  });
}

function invalidateLikes(queryClient: QueryClient, trackId: string) {
  void queryClient.invalidateQueries({ queryKey: socialKeys.engagement(trackId) });
  void queryClient.invalidateQueries({ queryKey: socialKeys.liked });
}

/**
 * Likes or unlikes a track. The heart flips immediately (liked-ids set and the track's counts) and is
 * restored if the server refuses.
 */
export function useToggleLike(trackId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (like: boolean) =>
      unwrap(
        like
          ? await api.PUT("/api/v1/tracks/{trackId}/like", { params: { path: { trackId } } })
          : await api.DELETE("/api/v1/tracks/{trackId}/like", { params: { path: { trackId } } }),
      ),
    onMutate: async (like) => {
      await queryClient.cancelQueries({ queryKey: socialKeys.likedIds });
      const previousIds = queryClient.getQueryData<Set<string>>(socialKeys.likedIds);
      const previousEngagement = queryClient.getQueryData<Engagement>(socialKeys.engagement(trackId));
      if (previousIds) {
        const next = new Set(previousIds);
        if (like) next.add(trackId);
        else next.delete(trackId);
        queryClient.setQueryData(socialKeys.likedIds, next);
      }
      if (previousEngagement && previousEngagement.liked !== like) {
        queryClient.setQueryData<Engagement>(socialKeys.engagement(trackId), {
          ...previousEngagement,
          liked: like,
          likeCount: Number(previousEngagement.likeCount) + (like ? 1 : -1),
        });
      }
      return { previousIds, previousEngagement };
    },
    onError: (_error, _like, context) => {
      if (context?.previousIds) queryClient.setQueryData(socialKeys.likedIds, context.previousIds);
      if (context?.previousEngagement)
        queryClient.setQueryData(socialKeys.engagement(trackId), context.previousEngagement);
    },
    onSuccess: (engagement) => queryClient.setQueryData(socialKeys.engagement(trackId), engagement),
    onSettled: () => invalidateLikes(queryClient, trackId),
  });
}

export function useFollowSummary(artistId: string) {
  return useQuery({
    queryKey: socialKeys.follow(artistId),
    queryFn: async ({ signal }) =>
      unwrap(
        await api.GET("/api/v1/artists/{artistId}/followers/summary", {
          params: { path: { artistId } },
          signal,
        }),
      ),
  });
}

export function useToggleFollow(artistId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (follow: boolean) =>
      unwrap(
        follow
          ? await api.PUT("/api/v1/artists/{artistId}/follow", { params: { path: { artistId } } })
          : await api.DELETE("/api/v1/artists/{artistId}/follow", { params: { path: { artistId } } }),
      ),
    onSuccess: (summary) => {
      queryClient.setQueryData(socialKeys.follow(artistId), summary);
      void queryClient.invalidateQueries({ queryKey: socialKeys.following });
    },
  });
}

function paged<T>(
  key: readonly unknown[],
  fetchPage: (
    page: number,
    signal: AbortSignal,
  ) => Promise<{ items: T[]; meta: { page: number; hasMore: boolean; total: number } }>,
  enabled = true,
) {
  return {
    queryKey: key,
    initialPageParam: 1,
    queryFn: ({ pageParam, signal }: { pageParam: number; signal: AbortSignal }) =>
      fetchPage(pageParam, signal),
    getNextPageParam: (last: { meta: { page: number; hasMore: boolean } }) =>
      last.meta.hasMore ? last.meta.page + 1 : undefined,
    enabled,
  };
}

export function useLikedTracks(enabled: boolean) {
  return useInfiniteQuery(
    paged(
      socialKeys.liked,
      async (page, signal) =>
        unwrapPage(
          await api.GET("/api/v1/me/likes", { params: { query: { page, pageSize: PAGE_SIZE } }, signal }),
        ),
      enabled,
    ),
  );
}

export function useHistory(enabled: boolean, pageSize = PAGE_SIZE) {
  return useInfiniteQuery(
    paged(
      [...socialKeys.history, pageSize],
      async (page, signal) =>
        unwrapPage(await api.GET("/api/v1/me/history", { params: { query: { page, pageSize } }, signal })),
      enabled,
    ),
  );
}

export function useFollowing(enabled: boolean) {
  return useInfiniteQuery(
    paged(
      socialKeys.following,
      async (page, signal) =>
        unwrapPage(
          await api.GET("/api/v1/me/following", { params: { query: { page, pageSize: PAGE_SIZE } }, signal }),
        ),
      enabled,
    ),
  );
}

export function useComments(trackId: string) {
  return useInfiniteQuery(
    paged(socialKeys.comments(trackId), async (page, signal) =>
      unwrapPage(
        await api.GET("/api/v1/tracks/{trackId}/comments", {
          params: { path: { trackId }, query: { page, pageSize: PAGE_SIZE } },
          signal,
        }),
      ),
    ),
  );
}

function useInvalidateComments(trackId: string) {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: socialKeys.comments(trackId) });
    void queryClient.invalidateQueries({ queryKey: socialKeys.engagement(trackId) });
  };
}

export function usePostComment(trackId: string) {
  const invalidate = useInvalidateComments(trackId);
  return useMutation({
    mutationFn: async (body: Schemas["PostCommentRequest"]) =>
      unwrap(await api.POST("/api/v1/tracks/{trackId}/comments", { params: { path: { trackId } }, body })),
    onSuccess: invalidate,
  });
}

export function useEditComment(trackId: string) {
  const invalidate = useInvalidateComments(trackId);
  return useMutation({
    mutationFn: async ({ commentId, body }: { commentId: string; body: string }) =>
      unwrap(
        await api.PATCH("/api/v1/comments/{commentId}", { params: { path: { commentId } }, body: { body } }),
      ),
    onSuccess: invalidate,
  });
}

export function useDeleteComment(trackId: string) {
  const invalidate = useInvalidateComments(trackId);
  return useMutation({
    mutationFn: async (commentId: string) =>
      ensureOk(await api.DELETE("/api/v1/comments/{commentId}", { params: { path: { commentId } } })),
    onSuccess: invalidate,
  });
}
