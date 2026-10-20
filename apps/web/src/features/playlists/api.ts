import {
  keepPreviousData,
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { api, ensureOk, unwrap, unwrapPage } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import type { Track } from "@/features/tracks/api";

type Schemas = components["schemas"];
export type Playlist = Schemas["PlaylistDto"];
export type PlaylistVisibility = Schemas["PlaylistVisibility"];
export type SavePlaylist = Schemas["SavePlaylistRequest"];

export const playlistKeys = {
  all: ["playlists"] as const,
  detail: (id: string) => ["playlists", "detail", id] as const,
  tracks: (id: string) => ["playlists", "tracks", id] as const,
  mine: (containsTrack?: string) => ["playlists", "mine", containsTrack ?? null] as const,
  public: (q: string) => ["playlists", "public", q] as const,
};

const MY_PAGE_SIZE = 50;

/** The signed-in user's playlists (newest first, up to 50). With a track id, each says whether it holds it. */
export function useMyPlaylists(enabled: boolean, containsTrack?: string) {
  return useQuery({
    queryKey: playlistKeys.mine(containsTrack),
    queryFn: async ({ signal }) =>
      unwrap(
        await api.GET("/api/v1/me/playlists", {
          params: { query: { pageSize: MY_PAGE_SIZE, containsTrack } },
          signal,
        }),
      ),
    enabled,
  });
}

/** Public playlists: matching q, or newest when q is empty. */
export function usePublicPlaylists(q: string, pageSize = 12) {
  return useInfiniteQuery({
    queryKey: [...playlistKeys.public(q), pageSize],
    initialPageParam: 1,
    queryFn: async ({ pageParam, signal }) =>
      unwrapPage(
        await api.GET("/api/v1/playlists", {
          params: { query: { q: q || undefined, page: pageParam, pageSize } },
          signal,
        }),
      ),
    getNextPageParam: (last) => (last.meta.hasMore ? last.meta.page + 1 : undefined),
    placeholderData: keepPreviousData,
  });
}

export function usePlaylist(playlistId: string) {
  return useQuery({
    queryKey: playlistKeys.detail(playlistId),
    queryFn: async ({ signal }) =>
      unwrap(await api.GET("/api/v1/playlists/{playlistId}", { params: { path: { playlistId } }, signal })),
  });
}

export function usePlaylistTracks(playlistId: string, enabled: boolean) {
  return useQuery({
    queryKey: playlistKeys.tracks(playlistId),
    queryFn: async ({ signal }): Promise<Track[]> =>
      unwrap(
        await api.GET("/api/v1/playlists/{playlistId}/tracks", { params: { path: { playlistId } }, signal }),
      ),
    enabled,
  });
}

function useInvalidatePlaylists() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: playlistKeys.all });
}

export function useCreatePlaylist() {
  const invalidate = useInvalidatePlaylists();
  return useMutation({
    mutationFn: async (body: SavePlaylist) => unwrap(await api.POST("/api/v1/playlists", { body })),
    onSuccess: invalidate,
  });
}

export function useUpdatePlaylist(playlistId: string) {
  const invalidate = useInvalidatePlaylists();
  return useMutation({
    mutationFn: async (body: SavePlaylist) =>
      unwrap(await api.PATCH("/api/v1/playlists/{playlistId}", { params: { path: { playlistId } }, body })),
    onSuccess: invalidate,
  });
}

export function useDeletePlaylist() {
  const invalidate = useInvalidatePlaylists();
  return useMutation({
    mutationFn: async (playlistId: string) =>
      ensureOk(await api.DELETE("/api/v1/playlists/{playlistId}", { params: { path: { playlistId } } })),
    onSuccess: invalidate,
  });
}

export function useAddToPlaylist() {
  const invalidate = useInvalidatePlaylists();
  return useMutation({
    mutationFn: async ({ playlistId, trackId }: { playlistId: string; trackId: string }) =>
      unwrap(
        await api.POST("/api/v1/playlists/{playlistId}/tracks", {
          params: { path: { playlistId } },
          body: { trackId },
        }),
      ),
    onSuccess: invalidate,
  });
}

export function useRemoveFromPlaylist(playlistId: string) {
  const invalidate = useInvalidatePlaylists();
  return useMutation({
    mutationFn: async (trackId: string) =>
      ensureOk(
        await api.DELETE("/api/v1/playlists/{playlistId}/tracks/{trackId}", {
          params: { path: { playlistId, trackId } },
        }),
      ),
    onSuccess: invalidate,
  });
}

/**
 * Saves a new order. The list is updated optimistically (so drag & drop feels instant) and restored if
 * the server rejects it.
 */
export function useReorderPlaylist(playlistId: string) {
  const queryClient = useQueryClient();
  const key = playlistKeys.tracks(playlistId);
  return useMutation({
    mutationFn: async (tracks: Track[]) =>
      unwrap(
        await api.PUT("/api/v1/playlists/{playlistId}/tracks/order", {
          params: { path: { playlistId } },
          body: { trackIds: tracks.map((t) => t.id) },
        }),
      ),
    onMutate: async (tracks) => {
      await queryClient.cancelQueries({ queryKey: key });
      const previous = queryClient.getQueryData<Track[]>(key);
      queryClient.setQueryData(key, tracks);
      return { previous };
    },
    onError: (_error, _tracks, context) => {
      if (context?.previous) queryClient.setQueryData(key, context.previous);
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: playlistKeys.all }),
  });
}
