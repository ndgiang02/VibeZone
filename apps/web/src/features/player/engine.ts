/**
 * The single playback engine: one HTMLAudioElement for the whole app (mounted by <PlayerEngine/> in the
 * root shell). The store sends commands here; media events flow back to the store. Track cards and
 * pages never create audio elements of their own.
 */
let audio: HTMLAudioElement | null = null;
let loadedTrackId: string | null = null;
let pendingSeek: number | null = null;
let onPlayRejected: (() => void) | null = null;

export const streamUrl = (trackId: string) => `/api/v1/tracks/${encodeURIComponent(trackId)}/stream`;

function play() {
  if (!audio) return;
  // Browsers reject play() without a user gesture (e.g. after a reload) — we never force autoplay.
  audio.play().catch(() => onPlayRejected?.());
}

export const engine = {
  attach(element: HTMLAudioElement, rejected: () => void) {
    audio = element;
    onPlayRejected = rejected;
  },

  detach(element: HTMLAudioElement) {
    if (audio === element) {
      audio = null;
      onPlayRejected = null;
    }
  },

  element: () => audio,

  /** The id of the track whose stream is loaded into the element (may be paused). */
  loadedTrackId: () => loadedTrackId,

  hasSource: (trackId: string) => loadedTrackId === trackId && Boolean(audio?.getAttribute("src")),

  load(trackId: string, autoplay: boolean, startAt = 0) {
    if (!audio) return;
    loadedTrackId = trackId;
    pendingSeek = startAt > 0 ? startAt : null;
    audio.src = streamUrl(trackId);
    audio.load();
    if (autoplay) play();
  },

  /** Applies a seek requested before metadata was available (called on loadedmetadata). */
  applyPendingSeek() {
    if (audio && pendingSeek != null) {
      audio.currentTime = pendingSeek;
      pendingSeek = null;
    }
  },

  play,

  pause() {
    audio?.pause();
  },

  seek(seconds: number) {
    if (!audio) return;
    if (audio.readyState >= HTMLMediaElement.HAVE_METADATA) {
      audio.currentTime = seconds;
    } else {
      pendingSeek = seconds;
    }
  },

  setVolume(volume: number, muted: boolean) {
    if (!audio) return;
    audio.volume = volume;
    audio.muted = muted;
  },

  /** Retry after a playback error: reload the same stream from the current position. */
  reload(startAt: number) {
    if (audio && loadedTrackId) engine.load(loadedTrackId, true, startAt);
  },

  stop() {
    if (!audio) return;
    audio.pause();
    audio.removeAttribute("src");
    audio.load();
    loadedTrackId = null;
    pendingSeek = null;
  },
};
