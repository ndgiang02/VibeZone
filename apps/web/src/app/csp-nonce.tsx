"use client";

import { setNonce } from "get-nonce";

/**
 * Hands this request's CSP nonce to libraries that inject <style> elements at runtime
 * (Radix → react-remove-scroll → react-style-singleton read it via get-nonce). Without it the
 * browser blocks those styles under style-src 'nonce-…'.
 */
export function CspNonce({ nonce }: { nonce: string | undefined }) {
  if (nonce) {
    setNonce(nonce);
  }
  return null;
}
