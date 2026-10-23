import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ensureOk, unwrap, unwrapPage } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

export type Notification = components["schemas"]["NotificationDto"];

export const notificationKeys = {
  all: ["notifications"] as const,
  unread: ["notifications", "unread"] as const,
  list: (pageSize: number) => ["notifications", "list", pageSize] as const,
};

/** Unread count. Pushed over SignalR (NotificationHub); the slow poll is only a fallback. */
export function useUnreadCount(enabled: boolean) {
  return useQuery({
    queryKey: notificationKeys.unread,
    queryFn: async ({ signal }) =>
      Number(unwrap(await api.GET("/api/v1/me/notifications/unread-count", { signal })).count),
    enabled,
    refetchInterval: 120_000,
  });
}

export function useNotifications(enabled: boolean, pageSize = 20) {
  return useInfiniteQuery({
    queryKey: notificationKeys.list(pageSize),
    initialPageParam: 1,
    queryFn: async ({ pageParam, signal }) =>
      unwrapPage(
        await api.GET("/api/v1/me/notifications", {
          params: { query: { page: pageParam, pageSize } },
          signal,
        }),
      ),
    getNextPageParam: (last) => (last.meta.hasMore ? last.meta.page + 1 : undefined),
    enabled,
  });
}

function useInvalidateNotifications() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: notificationKeys.all });
}

export function useMarkRead() {
  const invalidate = useInvalidateNotifications();
  return useMutation({
    mutationFn: async (notificationId: string) =>
      ensureOk(
        await api.POST("/api/v1/me/notifications/{notificationId}/read", {
          params: { path: { notificationId } },
        }),
      ),
    onSuccess: invalidate,
  });
}

export function useMarkAllRead() {
  const invalidate = useInvalidateNotifications();
  return useMutation({
    mutationFn: async () => ensureOk(await api.POST("/api/v1/me/notifications/read-all")),
    onSuccess: invalidate,
  });
}
