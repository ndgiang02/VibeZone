import { z } from "zod";

// Mirrors server rules (VibeZone.Modules.Identity: Handle, Validation). The server stays authoritative.
export const PASSWORD_MIN = 10;
export const PASSWORD_MAX = 128;
export const DISPLAY_NAME_MAX = 60;
const HANDLE_PATTERN = /^[a-z0-9][a-z0-9_-]{1,28}[a-z0-9]$/;

/** Already-translated validation messages (from the "validation" namespace). */
export type ValidationMessages = {
  required: string;
  email: string;
  passwordMin: string;
  passwordMax: string;
  handle: string;
  displayName: string;
  passwordsMismatch: string;
};

const password = (m: ValidationMessages) =>
  z.string().min(PASSWORD_MIN, m.passwordMin).max(PASSWORD_MAX, m.passwordMax);

const email = (m: ValidationMessages) => z.string().trim().min(1, m.required).pipe(z.email(m.email));

export const loginSchema = (m: ValidationMessages) =>
  z.object({
    email: email(m),
    password: z.string().min(1, m.required),
    rememberMe: z.boolean(),
  });

export const registerSchema = (m: ValidationMessages) =>
  z.object({
    email: email(m),
    handle: z
      .string()
      .trim()
      .transform((v) => v.toLowerCase())
      .pipe(z.string().regex(HANDLE_PATTERN, m.handle)),
    displayName: z.string().trim().max(DISPLAY_NAME_MAX, m.displayName),
    password: password(m),
  });

export const emailOnlySchema = (m: ValidationMessages) => z.object({ email: email(m) });

export const resetPasswordSchema = (m: ValidationMessages) =>
  z
    .object({ newPassword: password(m), confirmPassword: z.string() })
    .refine((v) => v.newPassword === v.confirmPassword, {
      message: m.passwordsMismatch,
      path: ["confirmPassword"],
    });

export const acceptInviteSchema = (m: ValidationMessages) =>
  z
    .object({ password: password(m), confirmPassword: z.string() })
    .refine((v) => v.password === v.confirmPassword, {
      message: m.passwordsMismatch,
      path: ["confirmPassword"],
    });

/** Admin inviting a staff member: like registration, without a password (the invitee chooses it). */
export const inviteStaffSchema = (m: ValidationMessages) =>
  z.object({
    email: email(m),
    handle: z
      .string()
      .trim()
      .transform((v) => v.toLowerCase())
      .pipe(z.string().regex(HANDLE_PATTERN, m.handle)),
    displayName: z.string().trim().max(DISPLAY_NAME_MAX, m.displayName),
    role: z.enum(["Moderator", "Admin"]),
  });

export const profileSchema = (m: ValidationMessages) =>
  z.object({
    displayName: z.string().trim().min(1, m.displayName).max(DISPLAY_NAME_MAX, m.displayName),
    locale: z.enum(["vi", "en"]),
  });

export const changePasswordSchema = (m: ValidationMessages) =>
  z.object({ currentPassword: z.string().min(1, m.required), newPassword: password(m) });

/** Only same-site relative paths are honoured after sign-in (prevents open redirects). */
export function safeNextPath(next: string | null | undefined): string | null {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/\\")) return null;
  return next;
}
