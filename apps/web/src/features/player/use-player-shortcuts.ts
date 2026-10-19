"use client";

import { useEffect } from "react";
import { usePlayerStore } from "./store";

function isTyping(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return target.isContentEditable || tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
}

/**
 * Global shortcuts (ignored while typing or with Ctrl/Meta/Alt):
 * Space/K play-pause · ←/J back 5 s · →/L forward 5 s · M mute · Shift+N next · Shift+P previous.
 * Focused buttons keep their own Space/Enter behaviour; sliders keep their arrow keys.
 */
export function usePlayerShortcuts() {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target))
        return;
      const store = usePlayerStore.getState();
      if (store.index < 0) return;
      const onButton = event.target instanceof HTMLButtonElement || event.target instanceof HTMLAnchorElement;
      const key = event.key.toLowerCase();

      if ((key === " " && !onButton) || key === "k") store.togglePlay();
      else if (key === "arrowleft" || key === "j") store.seekBy(-5);
      else if (key === "arrowright" || key === "l") store.seekBy(5);
      else if (key === "m") store.toggleMute();
      else if (key === "n" && event.shiftKey) store.next();
      else if (key === "p" && event.shiftKey) store.previous();
      else return;
      event.preventDefault();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);
}
