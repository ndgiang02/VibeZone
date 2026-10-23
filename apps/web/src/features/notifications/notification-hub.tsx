"use client";

import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useMe } from "@/features/auth/api";
import { notificationKeys } from "./api";

/**
 * Realtime notifications for the signed-in user: the API pushes `unread` ({ count }) over SignalR (same
 * origin, session cookie) and we refresh the lists. Reconnects on its own; renders nothing.
 */
export function NotificationHub() {
  const me = useMe();
  const queryClient = useQueryClient();
  const userId = me.data?.id;

  useEffect(() => {
    if (!userId) return;
    const connection = new HubConnectionBuilder()
      .withUrl("/api/v1/hubs/notifications")
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();

    connection.on("unread", (payload: { count: number | string }) => {
      queryClient.setQueryData(notificationKeys.unread, Number(payload.count));
      void queryClient.invalidateQueries({ queryKey: ["notifications", "list"] });
    });
    // After a reconnect we may have missed pushes.
    connection.onreconnected(() => void queryClient.invalidateQueries({ queryKey: notificationKeys.all }));

    // Hub unreachable (e.g. a proxy without WebSockets/SSE): the unread-count poll still works.
    connection.start().catch(() => undefined);

    return () => {
      void connection.stop();
    };
  }, [userId, queryClient]);

  return null;
}
