import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError, ensureOk, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];
export type Track = Schemas["TrackDto"];
export type Artist = Schemas["ArtistDto"];
export type ArtistProfile = Schemas["ArtistProfileDto"];
export type TrackVisibility = Schemas["TrackVisibility"];
export type UploadStatus = Schemas["UploadStatusDto"];
export type Waveform = { version: number; durationMs: number; peaks: number[] };

export const trackKeys = {
  all: ["tracks"] as const,
  newReleases: ["tracks", "new-releases"] as const,
  detail: (id: string) => ["tracks", "detail", id] as const,
  mine: ["tracks", "mine"] as const,
  upload: (id: string) => ["tracks", "upload", id] as const,
  waveform: (id: string) => ["tracks", "waveform", id] as const,
  artist: (slug: string) => ["artists", slug] as const,
  myArtist: ["artists", "me"] as const,
};

/** Numbers may arrive as strings (OpenAPI integer typing); normalise once here. */
export function durationMsOf(track: Pick<Track, "durationMs">): number | null {
  return track.durationMs == null ? null : Number(track.durationMs);
}

export function useNewReleases(limit = 24) {
  return useQuery({
    queryKey: [...trackKeys.newReleases, limit],
    queryFn: async ({ signal }) =>
      unwrap(await api.GET("/api/v1/tracks", { params: { query: { limit } }, signal })).items,
  });
}

export function useTrack(trackId: string) {
  return useQuery({
    queryKey: trackKeys.detail(trackId),
    queryFn: async ({ signal }) =>
      unwrap(await api.GET("/api/v1/tracks/{trackId}", { params: { path: { trackId } }, signal })),
  });
}

export function useArtistProfile(slug: string) {
  return useQuery({
    queryKey: trackKeys.artist(slug),
    queryFn: async ({ signal }) =>
      unwrap(await api.GET("/api/v1/artists/{slug}", { params: { path: { slug } }, signal })),
  });
}

/** The signed-in user's artist profile, or null if they haven't created one yet. */
export function useMyArtist(enabled: boolean) {
  return useQuery({
    queryKey: trackKeys.myArtist,
    queryFn: async ({ signal }) => {
      const result = await api.GET("/api/v1/me/artist", { signal });
      return result.response.status === 404 ? null : unwrap(result);
    },
    enabled,
  });
}

export function useCreateArtist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["CreateArtistRequest"]) =>
      unwrap(await api.POST("/api/v1/me/artist", { body })),
    onSuccess: (artist) => {
      queryClient.setQueryData(trackKeys.myArtist, artist);
      // Roles changed (Artist): refresh the account.
      void queryClient.invalidateQueries({ queryKey: ["auth", "me"] });
    },
  });
}

export function useMyTracks(enabled: boolean) {
  return useQuery({
    queryKey: trackKeys.mine,
    queryFn: async ({ signal }) => unwrap(await api.GET("/api/v1/me/tracks", { signal })).items,
    enabled,
    // While something is processing, refresh so the status flips to Ready/Failed without a reload.
    refetchInterval: (query) => (query.state.data?.some((t) => t.status === "Processing") ? 3000 : false),
  });
}

export function useCreateTrack() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["SaveTrackRequest"]) =>
      unwrap(await api.POST("/api/v1/tracks", { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: trackKeys.mine }),
  });
}

export function useDeleteTrack() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (trackId: string) =>
      ensureOk(await api.DELETE("/api/v1/tracks/{trackId}", { params: { path: { trackId } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: trackKeys.all }),
  });
}

export function useCancelUpload() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (trackId: string) =>
      ensureOk(await api.DELETE("/api/v1/tracks/{trackId}/upload", { params: { path: { trackId } } })),
    onSettled: () => queryClient.invalidateQueries({ queryKey: trackKeys.all }),
  });
}

/** Peaks for a Ready track (null while unavailable — never synthesised). */
export function useWaveform(trackId: string, enabled: boolean) {
  return useQuery({
    queryKey: trackKeys.waveform(trackId),
    queryFn: async ({ signal }): Promise<Waveform | null> => {
      const result = await api.GET("/api/v1/tracks/{trackId}/waveform", {
        params: { path: { trackId } },
        signal,
      });
      if (result.response.status === 404) return null;
      const data = unwrap(result);
      return {
        version: Number(data.version),
        durationMs: Number(data.durationMs),
        peaks: data.peaks.map(Number),
      };
    },
    enabled,
    staleTime: Infinity,
  });
}

export function isNotFound(error: unknown) {
  return error instanceof ApiError && error.status === 404;
}
