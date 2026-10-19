import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ensureOk, unwrap } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];
export type Me = Schemas["MeDto"];
export type Session = Schemas["SessionDto"];

export const authKeys = {
  all: ["auth"] as const,
  me: ["auth", "me"] as const,
  sessions: ["auth", "sessions"] as const,
};

/** The signed-in user, or null when anonymous (401 is an expected answer, not an error). */
export function useMe() {
  return useQuery({
    queryKey: authKeys.me,
    queryFn: async ({ signal }) => {
      const result = await api.GET("/api/v1/me", { signal });
      return result.response.status === 401 ? null : unwrap(result);
    },
    staleTime: 60_000,
  });
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["LoginRequest"]) =>
      ensureOk(await api.POST("/api/v1/auth/login", { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: authKeys.all }),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => ensureOk(await api.POST("/api/v1/auth/logout")),
    onSettled: () => {
      queryClient.setQueryData(authKeys.me, null);
      queryClient.removeQueries({ queryKey: authKeys.sessions });
    },
  });
}

export function useRegister() {
  return useMutation({
    mutationFn: async (body: Schemas["RegisterRequest"]) =>
      ensureOk(await api.POST("/api/v1/auth/register", { body })),
  });
}

export function useVerifyEmail() {
  return useMutation({
    mutationFn: async (body: Schemas["ConfirmEmailRequest"]) =>
      ensureOk(await api.POST("/api/v1/auth/verify-email", { body })),
  });
}

export function useResendVerification() {
  return useMutation({
    mutationFn: async (body: Schemas["EmailOnlyRequest"]) =>
      ensureOk(await api.POST("/api/v1/auth/resend-verification", { body })),
  });
}

export function useForgotPassword() {
  return useMutation({
    mutationFn: async (body: Schemas["EmailOnlyRequest"]) =>
      ensureOk(await api.POST("/api/v1/auth/forgot-password", { body })),
  });
}

export function useResetPassword() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["ResetPasswordRequest"]) =>
      ensureOk(await api.POST("/api/v1/auth/reset-password", { body })),
    // Every session was revoked server-side, including this browser's if it had one.
    onSuccess: () => queryClient.setQueryData(authKeys.me, null),
  });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["UpdateProfileRequest"]) =>
      unwrap(await api.PATCH("/api/v1/me/profile", { body })),
    onSuccess: (me) => queryClient.setQueryData(authKeys.me, me),
  });
}

export function useChangePassword() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["ChangePasswordRequest"]) =>
      ensureOk(await api.POST("/api/v1/me/password", { body })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: authKeys.sessions }),
  });
}

export function useSessions(enabled: boolean) {
  return useQuery({
    queryKey: authKeys.sessions,
    queryFn: async ({ signal }) => unwrap(await api.GET("/api/v1/me/sessions", { signal })).items,
    enabled,
  });
}

export function useRevokeSession() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (sessionId: string) =>
      ensureOk(await api.DELETE("/api/v1/me/sessions/{sessionId}", { params: { path: { sessionId } } })),
    onSettled: () => queryClient.invalidateQueries({ queryKey: authKeys.sessions }),
  });
}

export function useRevokeOtherSessions() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => unwrap(await api.POST("/api/v1/me/sessions/revoke-others")).revoked,
    onSettled: () => queryClient.invalidateQueries({ queryKey: authKeys.sessions }),
  });
}
