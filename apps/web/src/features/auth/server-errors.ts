import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { ApiError } from "@/lib/api/client";
import { errorCode, isErrorCode, type ErrorCode } from "@/lib/api/errors";

/**
 * Puts server field errors (problem.errors[field] = [code]) onto matching form fields.
 * Returns the code to show at form level when the error isn't tied to a known field.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  fields: readonly Path<T>[],
  setError: UseFormSetError<T>,
  translate: (code: ErrorCode) => string,
): ErrorCode | null {
  if (error instanceof ApiError) {
    let placed = false;
    for (const [field, code] of Object.entries(error.fieldErrors)) {
      if ((fields as readonly string[]).includes(field) && isErrorCode(code)) {
        setError(field as Path<T>, { type: "server", message: translate(code) }, { shouldFocus: !placed });
        placed = true;
      }
    }
    if (placed) return null;
  }
  return errorCode(error);
}
