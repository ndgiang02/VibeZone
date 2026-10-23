import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ensureOk, unwrap, unwrapPage } from "@/lib/api/client";
import type { components } from "@/lib/api/schema";

type Schemas = components["schemas"];
export type Report = Schemas["ReportDto"];
export type ReportStatus = NonNullable<Schemas["ReportStatus"]>;
export type ReportReason = Schemas["ReportReason"];
export type ReportSubjectType = Schemas["ReportSubjectType"];

export const REPORT_REASONS: ReportReason[] = ["Copyright", "Abuse", "Spam", "Other"];

export const moderationKeys = {
  all: ["moderation"] as const,
  reports: (status: ReportStatus | "all", page: number) => ["moderation", "reports", status, page] as const,
};

export function useCreateReport() {
  return useMutation({
    mutationFn: async (body: Schemas["CreateReportRequest"]) =>
      unwrap(await api.POST("/api/v1/reports", { body })),
  });
}

export function useReports(status: ReportStatus | "all", page: number, enabled: boolean) {
  return useQuery({
    queryKey: moderationKeys.reports(status, page),
    queryFn: async ({ signal }) =>
      unwrapPage(
        await api.GET("/api/v1/moderation/reports", {
          params: { query: { status: status === "all" ? undefined : status, page, pageSize: 20 } },
          signal,
        }),
      ),
    enabled,
    placeholderData: keepPreviousData,
  });
}

function useInvalidateModeration() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: moderationKeys.all });
    void queryClient.invalidateQueries({ queryKey: ["admin"] });
    void queryClient.invalidateQueries({ queryKey: ["tracks"] });
  };
}

export function useResolveReport() {
  const invalidate = useInvalidateModeration();
  return useMutation({
    mutationFn: async ({
      reportId,
      action,
      note,
    }: {
      reportId: string;
      action: "remove" | "dismiss";
      note: string;
    }) =>
      unwrap(
        await api.POST("/api/v1/moderation/reports/{reportId}/resolve", {
          params: { path: { reportId } },
          body: { action, note: note.trim() || null },
        }),
      ),
    onSuccess: invalidate,
  });
}

export function useRestoreContent() {
  const invalidate = useInvalidateModeration();
  return useMutation({
    mutationFn: async ({ subjectType, subjectId }: { subjectType: ReportSubjectType; subjectId: string }) =>
      ensureOk(
        await api.POST("/api/v1/moderation/content/{subjectType}/{subjectId}/restore", {
          params: { path: { subjectType, subjectId } },
        }),
      ),
    onSuccess: invalidate,
  });
}
