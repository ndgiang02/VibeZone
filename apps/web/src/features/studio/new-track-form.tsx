"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { Label } from "@/components/ui/label";
import type { ErrorCode } from "@/lib/api/errors";
import { applyServerErrors } from "@/features/auth/server-errors";
import { useGenres } from "@/features/catalog/use-genres";
import { useCreateTrack } from "@/features/tracks/api";

const selectClass = "h-10 rounded-lg border border-input bg-transparent px-3 text-sm dark:bg-input/30";

/** Step 1: track metadata (creates a Draft). Step 2 is the upload on the track's row. */
export function NewTrackForm({ onCreated }: { onCreated: (trackId: string) => void }) {
  const t = useTranslations("studio.newTrack");
  const tTrack = useTranslations("track");
  const tErrors = useTranslations("errors");
  const genres = useGenres();
  const create = useCreateTrack();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const schema = z.object({
    title: z.string().trim().min(1, t("titleInvalid")).max(120, t("titleInvalid")),
    genreSlug: z.string(),
    visibility: z.enum(["Public", "Unlisted", "Private"]),
    description: z.string().max(2000, t("descriptionTooLong")),
  });
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { title: "", genreSlug: "", visibility: "Public", description: "" },
  });

  const visibility = useWatch({ control: form.control, name: "visibility" });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      const track = await create.mutateAsync({
        title: values.title,
        genreSlug: values.genreSlug || null,
        visibility: values.visibility,
        description: values.description || null,
      });
      form.reset();
      onCreated(track.id);
    } catch (error) {
      setFormError(
        applyServerErrors(error, ["title", "genreSlug", "visibility", "description"], form.setError, (c) =>
          tErrors(c),
        ),
      );
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
      <TextField
        label={t("title")}
        error={form.formState.errors.title?.message}
        {...form.register("title")}
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="track-genre">{t("genre")}</Label>
          <select
            id="track-genre"
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
          <Label htmlFor="track-visibility">{t("visibility")}</Label>
          <select
            id="track-visibility"
            className={selectClass}
            aria-describedby="track-visibility-hint"
            {...form.register("visibility")}
          >
            <option value="Public">{tTrack("visibility.Public")}</option>
            <option value="Unlisted">{tTrack("visibility.Unlisted")}</option>
            <option value="Private">{tTrack("visibility.Private")}</option>
          </select>
          <p id="track-visibility-hint" className="text-xs text-muted-foreground">
            {t(`visibilityHint.${visibility}`)}
          </p>
        </div>
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="track-description">{t("description")}</Label>
        <textarea
          id="track-description"
          rows={3}
          className="rounded-lg border border-input bg-transparent px-3 py-2 text-sm dark:bg-input/30"
          aria-invalid={form.formState.errors.description ? true : undefined}
          {...form.register("description")}
        />
        {form.formState.errors.description ? (
          <p className="text-xs text-destructive">{form.formState.errors.description.message}</p>
        ) : null}
      </div>
      <SubmitButton pending={create.isPending} className="self-start">
        {t("submit")}
      </SubmitButton>
    </form>
  );
}
