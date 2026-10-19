import { ApiError, getCsrfToken, resetCsrfToken, type ProblemDetails } from "@/lib/api/client";

/** Must match the API default (Media:MaxUploadBytes). The server stays authoritative. */
export const MAX_UPLOAD_BYTES = 200 * 1024 * 1024;

export const ACCEPTED_AUDIO = "audio/*,.mp3,.wav,.flac,.m4a,.aac,.ogg,.oga,.opus,.aif,.aiff,.webm";

export type UploadHandle = { promise: Promise<void>; abort: () => void };

/**
 * Uploads the file as the raw request body. XHR (not fetch) because only XHR reports upload progress.
 * Sends the CSRF header, the exact size (the API rejects truncated bodies) and the display name.
 * Retries once on csrf_invalid, like the fetch client.
 */
export function uploadTrackAudio(
  trackId: string,
  file: File,
  onProgress: (fraction: number) => void,
): UploadHandle {
  let current: XMLHttpRequest | null = null;
  let aborted = false;

  const send = (token: string) =>
    new Promise<{ status: number; problem?: ProblemDetails }>((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      current = xhr;
      xhr.open("PUT", `/api/v1/tracks/${encodeURIComponent(trackId)}/upload`);
      xhr.setRequestHeader("Content-Type", "application/octet-stream");
      xhr.setRequestHeader("X-CSRF-TOKEN", token);
      xhr.setRequestHeader("X-Upload-Length", String(file.size));
      xhr.setRequestHeader("X-File-Name", encodeURIComponent(file.name));
      xhr.upload.onprogress = (event) => {
        if (event.lengthComputable) onProgress(event.loaded / event.total);
      };
      xhr.onload = () => {
        let problem: ProblemDetails | undefined;
        try {
          problem = xhr.status >= 400 ? (JSON.parse(xhr.responseText) as ProblemDetails) : undefined;
        } catch {
          problem = undefined;
        }
        resolve({ status: xhr.status, problem });
      };
      xhr.onerror = () => reject(new TypeError("Network error during upload"));
      xhr.onabort = () => reject(new DOMException("Upload aborted", "AbortError"));
      xhr.send(file);
    });

  const promise = (async () => {
    let result = await send(await getCsrfToken());
    if (result.status === 400 && result.problem?.code === "csrf_invalid" && !aborted) {
      resetCsrfToken();
      result = await send(await getCsrfToken());
    }
    if (result.status >= 400) {
      throw new ApiError(result.status, result.problem);
    }
    onProgress(1);
  })();

  return {
    promise,
    abort: () => {
      aborted = true;
      current?.abort();
    },
  };
}
