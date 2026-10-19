# ADR-0005: Audio delivery — authorised HTTP range streaming first, then signed URLs and HLS

- Status: Accepted (M0)
- Date: 2026-10-19

## Context

Playback must support seeking, be authorised (private/unlisted tracks), never load whole files
into memory, and later scale via CDN.

## Decision

- **Playback rendition**: AAC-LC in MP4 (`.m4a`, `+faststart`), 256 kbps stereo, normalised sample
  rate. Universally supported by `<audio>` in modern browsers. The original upload is kept
  private and never streamed.
- **MVP (M3)**: `GET /api/v1/tracks/{id}/stream` authorises, then forwards the client `Range` to
  S3 `GetObject` and streams the body back with correct `206`/`200`/`416`, `Content-Range`,
  `Content-Length`, `Accept-Ranges: bytes`, `Content-Type`, `ETag`. Single ranges only; multi-range
  requests get the full `200` response or `416` per RFC 9110 rules. Client disconnect cancels the
  upstream request.
- **Later**: short-lived pre-signed URLs (redirect) behind a CDN; then HLS (fMP4/AAC, multiple
  bitrates) via hls.js where needed.
- **Waveform**: peaks computed by the worker from decoded audio (FFmpeg → PCM → .NET peak
  extraction), stored as JSON in storage, rendered by wavesurfer.js. No synthetic waveforms.

## Consequences

- + Simple, correct, testable; authorization on every request.
- − API bandwidth carries audio in MVP; acceptable locally/early, replaced by signed URLs + CDN.
- Signed URLs are not DRM; they limit link sharing in time, not copying.
