"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import type { ErrorCode } from "@/lib/api/errors";
import { applyServerErrors } from "@/features/auth/server-errors";
import { useUpdateTrack } from "@/features/catalog/api";
import { useGenres } from "@/features/catalog/use-genres";
import type { Track } from "@/features/tracks/api";

const selectClass = "h-10 rounded-lg border border-input bg-transparent px-3 text-sm dark:bg-input/30";

/** Inline editor for a track's details (PATCH sends the full object). */
export function TrackEditForm({ track, onDone }: { track: Track; onDone: () => void }) {
  const t = useTranslations("studio.newTrack");
  const tEdit = useTranslations("studio.edit");
  const tTrack = useTranslations("track");
  const tCommon = useTranslations("common");
  const tErrors = useTranslations("errors");
  const genres = useGenres();
  const update = useUpdateTrack(track.id);
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const schema = z.object({
    title: z.string().trim().min(1, t("titleInvalid")).max(120, t("titleInvalid")),
    genreSlug: z.string(),
    visibility: z.enum(["Public", "Unlisted", "Private"]),
    description: z.string().max(2000, t("descriptionTooLong")),
  });
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: {
      title: track.title,
      genreSlug: track.genre?.slug ?? "",
      visibility: track.visibility,
      description: track.description ?? "",
    },
  });

  const submit = async (values: z.infer<typeof schema>) => {
    setFormError(null);
    try {
      await update.mutateAsync({
        title: values.title,
        genreSlug: values.genreSlug || null,
        visibility: values.visibility,
        description: values.description || null,
      });
      onDone();
    } catch (error) {
      setFormError(
        applyServerErrors(error, ["title", "genreSlug", "visibility", "description"], form.setError, (c) =>
          tErrors(c),
        ),
      );
    }
  };

  return (
    <form
      onSubmit={(event) => void form.handleSubmit(submit)(event)}
      noValidate
      className="flex animate-in flex-col gap-4 rounded-xl border bg-surface-card p-4 duration-300 fade-in slide-in-from-top-1"
    >
      {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
      <TextField
        label={t("title")}
        error={form.formState.errors.title?.message}
        {...form.register("title")}
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={`edit-genre-${track.id}`}>{t("genre")}</Label>
          <select
            id={`edit-genre-${track.id}`}
            className={selectClass}
            disabled={genres.isPending}
            {...form.register("genreSlug")}
          >
            <option value="">{t("noGenre")}</option>
            {genres.data?.map((genre) => (
              <option key={genre.slug} value={genre.slug}>
                {genre.name}
              </option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={`edit-vis-${track.id}`}>{t("visibility")}</Label>
          <select id={`edit-vis-${track.id}`} className={selectClass} {...form.register("visibility")}>
            <option value="Public">{tTrack("visibility.Public")}</option>
            <option value="Unlisted">{tTrack("visibility.Unlisted")}</option>
            <option value="Private">{tTrack("visibility.Private")}</option>
          </select>
        </div>
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={`edit-desc-${track.id}`}>{t("description")}</Label>
        <textarea
          id={`edit-desc-${track.id}`}
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
        <SubmitButton pending={update.isPending}>{tEdit("save")}</SubmitButton>
        <Button type="button" variant="ghost" size="lg" className="h-10" onClick={onDone}>
          {tCommon("cancel")}
        </Button>
      </div>
    </form>
  );
}
