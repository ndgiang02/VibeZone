import {
  keepPreviousData,
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { api, ensureOk, unwrap, unwrapPage } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { trackKeys } from "@/features/tracks/api";

type Schemas = components["schemas"];
export type Album = Schemas["AlbumDto"];
export type AlbumDetail = Schemas["AlbumDetailDto"];
export type AlbumKind = Schemas["AlbumKind"];
export type SaveAlbum = Schemas["SaveAlbumRequest"];
export type SaveTrack = Schemas["SaveTrackRequest"];
export type SearchResults = Schemas["SearchResultsDto"];
export type SearchKind = "tracks" | "artists" | "albums";

export const catalogKeys = {
  album: (id: string) => ["albums", id] as const,
  myAlbums: ["albums", "mine"] as const,
  search: (q: string) => ["search", q] as const,
  searchKind: (kind: SearchKind, q: string) => ["search", kind, q] as const,
};

const SEARCH_PAGE = 20;

/** Overview of every kind (6 each, with totals). Disabled for an empty query. */
export function useSearch(q: string) {
  return useQuery({
    queryKey: catalogKeys.search(q),
    queryFn: async ({ signal }) =>
      unwrap(await api.GET("/api/v1/search", { params: { query: { q } }, signal })),
    enabled: q.trim().length > 0,
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  });
}

/** All results of one kind, page by page ("Show more"). */
export function useSearchAll(kind: SearchKind, q: string, enabled: boolean) {
  return useInfiniteQuery({
    queryKey: catalogKeys.searchKind(kind, q),
    initialPageParam: 1,
    queryFn: async ({ pageParam, signal }) => {
      const params = { query: { q, page: pageParam, pageSize: SEARCH_PAGE } };
      switch (kind) {
        case "tracks":
          return unwrapPage(await api.GET("/api/v1/search/tracks", { params, signal }));
        case "artists":
          return unwrapPage(await api.GET("/api/v1/search/artists", { params, signal }));
        default:
          return unwrapPage(await api.GET("/api/v1/search/albums", { params, signal }));
      }
    },
    getNextPageParam: (last) => (last.meta.hasMore ? last.meta.page + 1 : undefined),
    enabled: enabled && q.trim().length > 0,
  });
}

export function useAlbum(albumId: string) {
  return useQuery({
    queryKey: catalogKeys.album(albumId),
    queryFn: async ({ signal }) =>
      unwrap(await api.GET("/api/v1/albums/{albumId}", { params: { path: { albumId } }, signal })),
  });
}

export function useMyAlbums(enabled: boolean) {
  return useQuery({
    queryKey: catalogKeys.myAlbums,
    queryFn: async ({ signal }) => unwrap(await api.GET("/api/v1/me/albums", { signal })),
    enabled,
  });
}

/** Albums and tracks show each other (counts, covers, album links), so refresh both. */
function useInvalidateCatalog() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: ["albums"] });
    void queryClient.invalidateQueries({ queryKey: trackKeys.all });
    void queryClient.invalidateQueries({ queryKey: ["artists"] });
  };
}

export function useCreateAlbum() {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: async (body: SaveAlbum) => unwrap(await api.POST("/api/v1/albums", { body })),
    onSuccess: invalidate,
  });
}

export function useUpdateAlbum(albumId: string) {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: async (body: SaveAlbum) =>
      unwrap(await api.PATCH("/api/v1/albums/{albumId}", { params: { path: { albumId } }, body })),
    onSuccess: invalidate,
  });
}

export function useDeleteAlbum() {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: async (albumId: string) =>
      ensureOk(await api.DELETE("/api/v1/albums/{albumId}", { params: { path: { albumId } } })),
    onSuccess: invalidate,
  });
}

export function useSetAlbumTracks(albumId: string) {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: async (trackIds: string[]) =>
      unwrap(
        await api.PUT("/api/v1/albums/{albumId}/tracks", {
          params: { path: { albumId } },
          body: { trackIds },
        }),
      ),
    onSuccess: invalidate,
  });
}

export function useUpdateTrack(trackId: string) {
  const invalidate = useInvalidateCatalog();
  return useMutation({
    mutationFn: async (body: SaveTrack) =>
      unwrap(await api.PATCH("/api/v1/tracks/{trackId}", { params: { path: { trackId } }, body })),
    onSuccess: invalidate,
  });
}
