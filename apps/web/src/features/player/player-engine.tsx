"use client";

import { useEffect, useRef } from "react";
import { engine } from "./engine";
import { selectCurrentTrack, usePlayerStore } from "./store";

/** Mounts the app's only <audio> element and mirrors its events into the player store. */
export function PlayerEngine() {
  const ref = useRef<HTMLAudioElement>(null);

  useEffect(() => {
    const audio = ref.current;
    if (!audio) return;
    const store = usePlayerStore;
    engine.attach(audio, () => store.getState().sync({ status: "paused" }));
    void Promise.resolve(store.persist.rehydrate()).then(() => {
      const { volume, muted } = store.getState();
      engine.setVolume(volume, muted);
    });

    const sync = store.getState().sync;
    const handlers: Array<[keyof HTMLMediaElementEventMap, () => void]> = [
      ["loadstart", () => sync({ status: audio.paused ? store.getState().status : "loading" })],
      ["waiting", () => sync({ status: "loading" })],
      ["playing", () => sync({ status: "playing" })],
      [
        "play",
        () => sync({ status: audio.readyState >= HTMLMediaElement.HAVE_FUTURE_DATA ? "playing" : "loading" }),
      ],
      ["pause", () => !audio.ended && sync({ status: "paused" })],
      ["loadedmetadata", () => engine.applyPendingSeek()],
      ["durationchange", () => Number.isFinite(audio.duration) && sync({ duration: audio.duration })],
      ["timeupdate", () => sync({ currentTime: audio.currentTime })],
      ["ended", () => store.getState().next(true)],
      ["error", () => audio.getAttribute("src") && sync({ status: "error" })],
    ];
    for (const [event, handler] of handlers) audio.addEventListener(event, handler);
    return () => {
      for (const [event, handler] of handlers) audio.removeEventListener(event, handler);
      engine.detach(audio);
    };
  }, []);

  useMediaSession();

  // preload="none": nothing is fetched until the user presses play.
  return <audio ref={ref} preload="none" hidden />;
}

/** OS / lock-screen controls (where supported). */
function useMediaSession() {
  const track = usePlayerStore(selectCurrentTrack);
  const status = usePlayerStore((s) => s.status);

  useEffect(() => {
    if (typeof navigator === "undefined" || !("mediaSession" in navigator)) return;
    const session = navigator.mediaSession;
    session.metadata = track
      ? new MediaMetadata({ title: track.title, artist: track.artistName, album: "VibeZone" })
      : null;
    const store = usePlayerStore.getState;
    session.setActionHandler("play", () => store().togglePlay());
    session.setActionHandler("pause", () => store().togglePlay());
    session.setActionHandler("nexttrack", () => store().next());
    session.setActionHandler("previoustrack", () => store().previous());
    session.setActionHandler(
      "seekto",
      (details) => details.seekTime != null && store().seek(details.seekTime),
    );
  }, [track]);

  useEffect(() => {
    if (typeof navigator === "undefined" || !("mediaSession" in navigator)) return;
    navigator.mediaSession.playbackState =
      status === "playing" ? "playing" : status === "paused" ? "paused" : "none";
  }, [status]);
}
