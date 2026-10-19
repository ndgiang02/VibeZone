import { useQuery } from "@tanstack/react-query";
import { api, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Genre = components["schemas"]["GenreDto"];

export const genreKeys = { all: ["genres"] as const };

export function useGenres() {
  return useQuery({
    queryKey: genreKeys.all,
    queryFn: async ({ signal }) => unwrap(await api.GET("/api/v1/genres", { signal })).items,
    staleTime: 5 * 60_000,
  });
}
