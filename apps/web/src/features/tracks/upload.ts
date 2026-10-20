import { ApiError, getCsrfToken, resetCsrfToken, type ProblemDetails } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

/** Must match the API default (Media:MaxUploadBytes). The server stays authoritative. */
export const MAX_UPLOAD_BYTES = 200 * 1024 * 1024;

/** Must match the API default (Media:MaxArtworkBytes). */
export const MAX_ARTWORK_BYTES = 10 * 1024 * 1024;

export const ACCEPTED_AUDIO = "audio/*,.mp3,.wav,.flac,.m4a,.aac,.ogg,.oga,.opus,.aif,.aiff,.webm";

/** JPEG, PNG and WebP only (the API rejects everything else, including SVG and GIF). */
export const ACCEPTED_ARTWORK = "image/jpeg,image/png,image/webp";

export type UploadStatus = components["schemas"]["UploadStatusDto"];
export type TrackArtwork = components["schemas"]["TrackArtworkDto"];

export type UploadProgress = { loaded: number; total: number };

export type UploadHandle<T> = { promise: Promise<T>; abort: () => void };

type Envelope<T> = { data: T };

/**
 * Sends a body with XHR (not fetch) because only XHR reports upload progress. Adds the CSRF header and
 * retries once on csrf_invalid, like the fetch client. Resolves with the response's `data`.
 */
function send<T>(
  method: "POST" | "PUT",
  url: string,
  body: XMLHttpRequestBodyInit,
  headers: Record<string, string>,
  onProgress?: (progress: UploadProgress) => void,
): UploadHandle<T> {
  let current: XMLHttpRequest | null = null;
  let aborted = false;

  const once = (token: string) =>
    new Promise<{ status: number; text: string }>((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      current = xhr;
      xhr.open(method, url);
      xhr.setRequestHeader("X-CSRF-TOKEN", token);
      for (const [name, value] of Object.entries(headers)) xhr.setRequestHeader(name, value);
      xhr.upload.onprogress = (event) => {
        if (event.lengthComputable) onProgress?.({ loaded: event.loaded, total: event.total });
      };
      xhr.onload = () => resolve({ status: xhr.status, text: xhr.responseText });
      xhr.onerror = () => reject(new TypeError("Network error during upload"));
      xhr.onabort = () => reject(new DOMException("Upload aborted", "AbortError"));
      xhr.send(body);
    });

  const parse = (text: string): unknown => {
    try {
      return JSON.parse(text) as unknown;
    } catch {
      return undefined;
    }
  };

  const promise = (async () => {
    let result = await once(await getCsrfToken());
    if (
      result.status === 400 &&
      (parse(result.text) as ProblemDetails | undefined)?.code === "csrf_invalid" &&
      !aborted
    ) {
      resetCsrfToken();
      result = await once(await getCsrfToken());
    }
    if (result.status >= 400) {
      throw new ApiError(result.status, parse(result.text) as ProblemDetails | undefined);
    }
    return (parse(result.text) as Envelope<T> | undefined)?.data as T;
  })();

  return {
    promise,
    abort: () => {
      aborted = true;
      current?.abort();
    },
  };
}

/**
 * Re-uploads audio for an existing Draft/Failed/Cancelled track as the raw request body, with the exact
 * size (the API rejects truncated bodies) and the display name.
 */
export function uploadTrackAudio(
  trackId: string,
  file: File,
  onProgress: (fraction: number) => void,
): UploadHandle<UploadStatus> {
  return send<UploadStatus>(
    "PUT",
    `/api/v1/tracks/${encodeURIComponent(trackId)}/upload`,
    file,
    {
      "Content-Type": "application/octet-stream",
      "X-Upload-Length": String(file.size),
      "X-File-Name": encodeURIComponent(file.name),
    },
    (p) => onProgress(p.loaded / p.total),
  );
}

export type NewTrackFields = {
  title: string;
  visibility: "Public" | "Unlisted" | "Private";
  genreSlug: string | null;
  description: string | null;
};

/**
 * Creates the track with its audio and optional cover in one multipart request — all or nothing on the
 * server. Text fields go first so the server reads them before the large file.
 */
export function uploadNewTrack(
  fields: NewTrackFields,
  audio: File,
  artwork: File | null,
  onProgress: (progress: UploadProgress) => void,
): UploadHandle<UploadStatus> {
  const form = new FormData();
  form.append("title", fields.title);
  form.append("visibility", fields.visibility);
  if (fields.genreSlug) form.append("genreSlug", fields.genreSlug);
  if (fields.description) form.append("description", fields.description);
  if (artwork) form.append("artwork", artwork, artwork.name);
  form.append("audio", audio, audio.name);
  // No Content-Type: the browser sets multipart/form-data with the boundary.
  return send<UploadStatus>("POST", "/api/v1/track-uploads", form, {}, onProgress);
}

/** Sets or replaces a track's cover (raw image body). */
export function uploadArtwork(trackId: string, image: File): UploadHandle<TrackArtwork> {
  return send<TrackArtwork>("PUT", `/api/v1/tracks/${encodeURIComponent(trackId)}/artwork`, image, {
    "Content-Type": image.type || "application/octet-stream",
  });
}

export type ArtistImageSlot = "avatar" | "banner";
export type ImageUrls = components["schemas"]["ImageUrlsDto"];

/** Sets or replaces the signed-in artist's avatar or banner (raw image body). */
export function uploadArtistImage(slot: ArtistImageSlot, image: File): UploadHandle<ImageUrls> {
  return send<ImageUrls>("PUT", `/api/v1/me/artist/${slot}`, image, {
    "Content-Type": image.type || "application/octet-stream",
  });
}

/** Sets or replaces the signed-in user's account picture (raw image body). */
export function uploadUserAvatar(image: File): UploadHandle<ImageUrls> {
  return send<ImageUrls>("PUT", "/api/v1/me/avatar", image, {
    "Content-Type": image.type || "application/octet-stream",
  });
}
