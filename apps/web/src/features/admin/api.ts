import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap, unwrapPage } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];
export type AdminUser = Schemas["AdminUserDto"];
export type AdminJob = Schemas["AdminJobDto"];
export type AuditEntry = Schemas["AuditEntryDto"];
export type JobStatus = NonNullable<Schemas["UploadJobStatus"]>;

const PAGE_SIZE = 20;

export const adminKeys = {
  all: ["admin"] as const,
  users: (q: string, role: string, page: number) => ["admin", "users", q, role, page] as const,
  tracks: (q: string, takenDown: boolean | null, page: number) =>
    ["admin", "tracks", q, takenDown, page] as const,
  jobs: (status: string, page: number) => ["admin", "jobs", status, page] as const,
  jobSummary: ["admin", "jobs", "summary"] as const,
  outbox: ["admin", "outbox"] as const,
  audit: (page: number) => ["admin", "audit", page] as const,
};

export function useAdminUsers(q: string, role: string, page: number) {
  return useQuery({
    queryKey: adminKeys.users(q, role, page),
    queryFn: async ({ signal }) =>
      unwrapPage(
        await api.GET("/api/v1/admin/users", {
          params: { query: { q: q || undefined, role: role || undefined, page, pageSize: PAGE_SIZE } },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useSetRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ userId, role, grant }: { userId: string; role: string; grant: boolean }) =>
      unwrap(
        grant
          ? await api.PUT("/api/v1/admin/users/{userId}/roles/{role}", { params: { path: { userId, role } } })
          : await api.DELETE("/api/v1/admin/users/{userId}/roles/{role}", {
              params: { path: { userId, role } },
            }),
      ),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: adminKeys.all }),
  });
}

export function useInviteStaff() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Schemas["InviteStaffRequest"]) =>
      unwrap(await api.POST("/api/v1/admin/users", { body })),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: adminKeys.all }),
  });
}

export function useAdminTracks(q: string, takenDown: boolean | null, page: number) {
  return useQuery({
    queryKey: adminKeys.tracks(q, takenDown, page),
    queryFn: async ({ signal }) =>
      unwrapPage(
        await api.GET("/api/v1/admin/tracks", {
          params: {
            query: { q: q || undefined, takenDown: takenDown ?? undefined, page, pageSize: PAGE_SIZE },
          },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useAdminJobs(status: JobStatus | "", page: number) {
  return useQuery({
    queryKey: adminKeys.jobs(status, page),
    queryFn: async ({ signal }) =>
      unwrapPage(
        await api.GET("/api/v1/admin/jobs", {
          params: { query: { status: status || undefined, page, pageSize: PAGE_SIZE } },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

export function useJobSummary() {
  return useQuery({
    queryKey: adminKeys.jobSummary,
    queryFn: async ({ signal }) => unwrap(await api.GET("/api/v1/admin/jobs/summary", { signal })),
  });
}

export function useOutboxSummary() {
  return useQuery({
    queryKey: adminKeys.outbox,
    queryFn: async ({ signal }) => unwrap(await api.GET("/api/v1/admin/outbox", { signal })),
  });
}

export function useAuditLog(page: number) {
  return useQuery({
    queryKey: adminKeys.audit(page),
    queryFn: async ({ signal }) =>
      unwrapPage(
        await api.GET("/api/v1/admin/audit-log", {
          params: { query: { page, pageSize: PAGE_SIZE } },
          signal,
        }),
      ),
    placeholderData: keepPreviousData,
  });
}
