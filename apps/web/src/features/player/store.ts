import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { engine } from "./engine";

/** What the player needs to show and stream a track (a snapshot; not refreshed from the API). */
export type PlayerTrack = {
  id: string;
  title: string;
  artistName: string;
  artistSlug: string;
  durationMs: number | null;
};

export type RepeatMode = "off" | "all" | "one";
export type PlaybackStatus = "idle" | "loading" | "playing" | "paused" | "error";

type PlayerState = {
  queue: PlayerTrack[];
  /** Index into `queue` of the current track, or -1. */
  index: number;
  /** Play order when shuffling (indices into `queue`); identity order otherwise. */
  order: number[];
  status: PlaybackStatus;
  currentTime: number;
  duration: number;
  volume: number;
  muted: boolean;
  shuffle: boolean;
  repeat: RepeatMode;
};

type PlayerActions = {
  /** Replaces the queue and starts playing `startIndex` (user gesture → allowed to play). */
  playQueue: (tracks: PlayerTrack[], startIndex?: number) => void;
  /** Plays a single track; if it's already current, toggles play/pause. */
  playTrack: (track: PlayerTrack, context?: PlayerTrack[]) => void;
  togglePlay: () => void;
  next: (auto?: boolean) => void;
  previous: () => void;
  seek: (seconds: number) => void;
  seekBy: (deltaSeconds: number) => void;
  setVolume: (volume: number) => void;
  toggleMute: () => void;
  toggleShuffle: () => void;
  cycleRepeat: () => void;
  enqueue: (track: PlayerTrack) => void;
  removeAt: (queueIndex: number) => void;
  jumpTo: (queueIndex: number) => void;
  clearQueue: () => void;
  /** Called by the engine from media element events. */
  sync: (patch: Partial<Pick<PlayerState, "status" | "currentTime" | "duration">>) => void;
};

export type PlayerStore = PlayerState & PlayerActions;

const identity = (length: number) => Array.from({ length }, (_, i) => i);

/** Fisher–Yates shuffle that keeps `first` at the front (the track playing now stays current). */
function shuffledOrder(length: number, first: number) {
  const rest = identity(length).filter((i) => i !== first);
  for (let i = rest.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [rest[i], rest[j]] = [rest[j]!, rest[i]!];
  }
  return first >= 0 ? [first, ...rest] : rest;
}

export const usePlayerStore = create<PlayerStore>()(
  persist(
    (set, get) => {
      const start = (index: number) => {
        const track = get().queue[index];
        if (!track) return;
        set({ index, status: "loading", currentTime: 0, duration: (track.durationMs ?? 0) / 1000 });
        engine.load(track.id, true);
      };

      const step = (direction: 1 | -1, wrap: boolean) => {
        const { order, index } = get();
        const position = order.indexOf(index);
        let target = position + direction;
        if (target >= order.length || target < 0) {
          if (!wrap) return false;
          target = (target + order.length) % order.length;
        }
        start(order[target]!);
        return true;
      };

      return {
        queue: [],
        index: -1,
        order: [],
        status: "idle",
        currentTime: 0,
        duration: 0,
        volume: 0.8,
        muted: false,
        shuffle: false,
        repeat: "off",

        playQueue: (tracks, startIndex = 0) => {
          if (tracks.length === 0) return;
          const index = Math.min(Math.max(startIndex, 0), tracks.length - 1);
          set({
            queue: tracks,
            order: get().shuffle ? shuffledOrder(tracks.length, index) : identity(tracks.length),
          });
          start(index);
        },

        playTrack: (track, context) => {
          const { queue, index } = get();
          if (queue[index]?.id === track.id) {
            get().togglePlay();
            return;
          }
          const tracks = context && context.some((t) => t.id === track.id) ? context : [track];
          get().playQueue(
            tracks,
            tracks.findIndex((t) => t.id === track.id),
          );
        },

        togglePlay: () => {
          const { status, index, queue } = get();
          if (index < 0 || !queue[index]) return;
          if (status === "playing" || status === "loading") {
            engine.pause();
          } else if (!engine.hasSource(queue[index].id)) {
            // Restored from storage: nothing loaded yet, and only now do we have a user gesture.
            set({ status: "loading" });
            engine.load(queue[index].id, true, get().currentTime);
          } else {
            engine.play();
          }
        },

        next: (auto = false) => {
          const { repeat } = get();
          if (auto && repeat === "one") {
            engine.seek(0);
            engine.play();
            return;
          }
          if (!step(1, repeat === "all") && auto) {
            // End of the queue: stay on the last track, paused at its end.
            set({ status: "paused" });
          }
        },

        previous: () => {
          // Like most players: restart the track unless we're right at its beginning.
          if (get().currentTime > 3 || !step(-1, get().repeat === "all")) {
            engine.seek(0);
          }
        },

        seek: (seconds) => {
          const { duration } = get();
          const clamped = Math.max(0, duration > 0 ? Math.min(seconds, duration) : seconds);
          set({ currentTime: clamped });
          engine.seek(clamped);
        },

        seekBy: (delta) => get().seek(get().currentTime + delta),

        setVolume: (volume) => {
          const v = Math.min(Math.max(volume, 0), 1);
          set({ volume: v, muted: v === 0 });
          engine.setVolume(v, v === 0);
        },

        toggleMute: () => {
          const muted = !get().muted;
          set({ muted });
          engine.setVolume(get().volume, muted);
        },

        toggleShuffle: () => {
          const { shuffle, queue, index } = get();
          set({
            shuffle: !shuffle,
            order: !shuffle ? shuffledOrder(queue.length, index) : identity(queue.length),
          });
        },

        cycleRepeat: () =>
          set({ repeat: get().repeat === "off" ? "all" : get().repeat === "all" ? "one" : "off" }),

        enqueue: (track) => {
          const { queue, order } = get();
          if (queue.some((t) => t.id === track.id)) return;
          set({ queue: [...queue, track], order: [...order, queue.length] });
          if (get().index < 0) {
            set({ index: 0, status: "paused", currentTime: 0, duration: (track.durationMs ?? 0) / 1000 });
          }
        },

        removeAt: (queueIndex) => {
          const { queue, index, order } = get();
          if (queueIndex < 0 || queueIndex >= queue.length) return;
          const nextQueue = queue.filter((_, i) => i !== queueIndex);
          const nextOrder = order.filter((i) => i !== queueIndex).map((i) => (i > queueIndex ? i - 1 : i));
          if (queueIndex === index) {
            engine.stop();
            set({
              queue: nextQueue,
              order: nextOrder,
              index: -1,
              status: "idle",
              currentTime: 0,
              duration: 0,
            });
            return;
          }
          set({ queue: nextQueue, order: nextOrder, index: queueIndex < index ? index - 1 : index });
        },

        jumpTo: (queueIndex) => start(queueIndex),

        clearQueue: () => {
          engine.stop();
          set({ queue: [], order: [], index: -1, status: "idle", currentTime: 0, duration: 0 });
        },

        sync: (patch) => set(patch),
      };
    },
    {
      name: "vibezone-player",
      version: 1,
      // Preferences and the queue survive reloads; playback never resumes on its own (no autoplay).
      storage: createJSONStorage(() => localStorage),
      // Rehydrated by <PlayerEngine/> after mount so server and first client render match.
      skipHydration: true,
      partialize: ({ queue, index, order, volume, muted, shuffle, repeat, currentTime }) => ({
        queue,
        index,
        order,
        volume,
        muted,
        shuffle,
        repeat,
        currentTime,
      }),
      merge: (persisted, current) => {
        const saved = persisted as Partial<PlayerState> | undefined;
        return { ...current, ...saved, status: saved?.index != null && saved.index >= 0 ? "paused" : "idle" };
      },
    },
  ),
);

export const selectCurrentTrack = (state: PlayerStore): PlayerTrack | null =>
  state.queue[state.index] ?? null;
