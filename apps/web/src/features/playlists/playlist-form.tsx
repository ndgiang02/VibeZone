"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import type { ErrorCode } from "@/lib/api/errors";
import { applyServerErrors } from "@/features/auth/server-errors";
import { type Playlist, useCreatePlaylist, useUpdatePlaylist } from "./api";

const selectClass = "h-10 rounded-lg border border-input bg-transparent px-3 text-sm dark:bg-input/30";

/** Create a playlist (no `playlist`) or edit one. */
export function PlaylistForm({
  playlist,
  onDone,
}: {
  playlist?: Playlist;
  onDone?: (playlist: Playlist) => void;
}) {
  const t = useTranslations("playlist");
  const tCommon = useTranslations("common");
  const tErrors = useTranslations("errors");
  const create = useCreatePlaylist();
  const update = useUpdatePlaylist(playlist?.id ?? "");
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const schema = z.object({
    title: z.string().trim().min(1, t("titleInvalid")).max(100, t("titleInvalid")),
    description: z.string().max(1000, t("descriptionTooLong")),
    visibility: z.enum(["Public", "Private"]),
  });
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: {
      title: playlist?.title ?? "",
      description: playlist?.description ?? "",
      visibility: playlist?.visibility ?? "Private",
    },
  });
  const visibility = useWatch({ control: form.control, name: "visibility" });

  const submit = async (values: z.infer<typeof schema>) => {
    setFormError(null);
    const body = {
      title: values.title,
      description: values.description || null,
      visibility: values.visibility,
    };
    try {
      const saved = playlist ? await update.mutateAsync(body) : await create.mutateAsync(body);
      if (!playlist) form.reset();
      onDone?.(saved);
    } catch (error) {
      setFormError(
        applyServerErrors(error, ["title", "description", "visibility"], form.setError, (c) => tErrors(c)),
      );
    }
  };

  return (
    <form
      onSubmit={(event) => void form.handleSubmit(submit)(event)}
      noValidate
      className="flex flex-col gap-4"
    >
      {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
      <TextField
        label={t("title")}
        maxLength={100}
        error={form.formState.errors.title?.message}
        {...form.register("title")}
      />
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={playlist ? `pl-vis-${playlist.id}` : "pl-vis-new"}>{t("visibilityLabel")}</Label>
        <select
          id={playlist ? `pl-vis-${playlist.id}` : "pl-vis-new"}
          className={selectClass}
          {...form.register("visibility")}
        >
          <option value="Private">{t("visibility.Private")}</option>
          <option value="Public">{t("visibility.Public")}</option>
        </select>
        <p className="text-xs text-muted-foreground">{t(`visibilityHint.${visibility}`)}</p>
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={playlist ? `pl-desc-${playlist.id}` : "pl-desc-new"}>{t("description")}</Label>
        <textarea
          id={playlist ? `pl-desc-${playlist.id}` : "pl-desc-new"}
          rows={3}
          className="rounded-lg border border-input bg-transparent px-3 py-2 text-sm dark:bg-input/30"
          aria-invalid={form.formState.errors.description ? true : undefined}
          {...form.register("description")}
        />
        {form.formState.errors.description ? (
          <p className="text-xs text-destructive">{form.formState.errors.description.message}</p>
        ) : null}
      </div>
      <div className="flex gap-2">
        <SubmitButton pending={create.isPending || update.isPending}>
          {playlist ? t("save") : t("create")}
        </SubmitButton>
        {playlist ? (
          <Button type="button" variant="ghost" size="lg" className="h-10" onClick={() => onDone?.(playlist)}>
            {tCommon("cancel")}
          </Button>
        ) : null}
      </div>
    </form>
  );
}
