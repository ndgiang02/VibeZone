import en from "../../../messages/en.json";
import { ApiError } from "./client";

/** Every API error code the UI can translate (keys of messages/*.json → "errors"). */
export type ErrorCode = keyof (typeof en)["errors"];

const KNOWN_CODES = new Set<string>(Object.keys(en.errors));

export function isErrorCode(code: string): code is ErrorCode {
  return KNOWN_CODES.has(code);
}

/** Maps any thrown value to a translatable code; unknown server codes degrade to "unknown". */
export function errorCode(error: unknown): ErrorCode {
  if (error instanceof ApiError) {
    return isErrorCode(error.code) ? error.code : "unknown";
  }
  if (error instanceof TypeError) {
    return "network"; // fetch rejects with TypeError when the network/proxy is unreachable
  }
  return "unknown";
}
