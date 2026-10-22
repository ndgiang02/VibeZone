"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { api } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";
import { socialKeys } from "@/features/social/api";
import { randomId } from "@/lib/utils";
import { usePlayerStore } from "./store";

type PlayEvent = components["schemas"]["PlayEventRequest"];

const CLIENT_ID_KEY = "vibezone-listener";
const FLUSH_MS = 10_000;
const PROGRESS_MS = 15_000;
const MAX_QUEUE = 200;

/** A random id that lets the server de-duplicate anonymous listens. Not a credential; never sent elsewhere. */
function clientId(): string | null {
  try {
    let id = localStorage.getItem(CLIENT_ID_KEY);
    if (!id) {
      id = randomId();
      localStorage.setItem(CLIENT_ID_KEY, id);
    }
    return id;
  } catch {
    return null;
  }
}

/**
 * Reports playback to the API (Analytics): `started` when a track begins, `progress` every 15 s of actual
 * listening and `completed` at the end. Listening time only grows while audio plays (seeks don't count);
 * the server decides what is a valid listen. Events are batched and retried with the same ids (idempotent).
 * Renders nothing; mounted once next to the player engine.
 */
export function PlayReporter() {
  const queryClient = useQueryClient();

  useEffect(() => {
    let queue: PlayEvent[] = [];
    let session: { id: string; trackId: string; listenedMs: number; lastReportedMs: number } | null = null;
    let lastTime = 0;
    let flushing = false;

    const push = (type: "started" | "progress" | "completed", positionSeconds: number) => {
      if (!session) return;
      queue.push({
        id: randomId(),
        sessionId: session.id,
        trackId: session.trackId,
        type,
        positionMs: Math.max(0, Math.floor(positionSeconds * 1000)),
        listenedMs: Math.floor(session.listenedMs),
        clientTimestamp: new Date().toISOString(),
      });
      session.lastReportedMs = session.listenedMs;
      if (queue.length > MAX_QUEUE) queue = queue.slice(-MAX_QUEUE);
    };

    const flush = async () => {
      if (flushing || queue.length === 0) return;
      flushing = true;
      const batch = queue.slice(0, 50);
      try {
        const result = await api.POST("/api/v1/plays", { body: { clientId: clientId(), events: batch } });
        // 4xx: drop the batch (it will never succeed); network/5xx: keep it for the next flush.
        if (result.response.ok || (result.response.status >= 400 && result.response.status < 500)) {
          queue = queue.slice(batch.length);
          if (result.response.ok) void queryClient.invalidateQueries({ queryKey: socialKeys.history });
        }
      } catch {
        // offline: retry later with the same event ids
      } finally {
        flushing = false;
      }
    };

    const unsubscribe = usePlayerStore.subscribe((state, previous) => {
      const trackId = state.queue[state.index]?.id ?? null;
      const previousId = previous.queue[previous.index]?.id ?? null;

      // A new playback: a different track, or the same one restarted from the beginning (repeat).
      const restarted =
        trackId === previousId &&
        previous.currentTime > 5 &&
        state.currentTime < 1 &&
        state.status === "playing";
      if (
        trackId &&
        (trackId !== session?.trackId || restarted) &&
        (state.status === "playing" || state.status === "loading")
      ) {
        if (session && session.listenedMs > session.lastReportedMs) push("progress", previous.currentTime);
        session = { id: randomId(), trackId, listenedMs: 0, lastReportedMs: 0 };
        lastTime = state.currentTime;
        push("started", state.currentTime);
        void flush();
        return;
      }

      if (!session || trackId !== session.trackId) return;

      if (state.status === "playing" && state.currentTime !== lastTime) {
        const delta = state.currentTime - lastTime;
        // Normal playback advances by small steps; jumps are seeks and don't count as listening.
        if (delta > 0 && delta < 2) session.listenedMs += delta * 1000;
        if (session.listenedMs - session.lastReportedMs >= PROGRESS_MS) push("progress", state.currentTime);
      }
      lastTime = state.currentTime;

      if (
        state.duration > 0 &&
        previous.currentTime < state.duration - 0.5 &&
        state.currentTime >= state.duration - 0.5
      ) {
        push("completed", state.currentTime);
        void flush();
      }
    });

    const timer = window.setInterval(() => void flush(), FLUSH_MS);
    const onHide = () => {
      if (document.visibilityState === "hidden") {
        if (session && session.listenedMs > session.lastReportedMs) push("progress", lastTime);
        void flush();
      }
    };
    document.addEventListener("visibilitychange", onHide);

    return () => {
      unsubscribe();
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", onHide);
    };
  }, [queryClient]);

  return null;
}
