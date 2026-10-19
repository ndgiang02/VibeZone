"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Mic2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { Label } from "@/components/ui/label";
import type { ErrorCode } from "@/lib/api/errors";
import { applyServerErrors } from "@/features/auth/server-errors";
import { useCreateArtist } from "@/features/tracks/api";

/** URL-safe slug suggestion from a display name (diacritics stripped, e.g. "Sơn Tùng" → "son-tung"). */
export function slugify(name: string) {
  return name
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 40);
}

export function BecomeArtistForm() {
  const t = useTranslations("studio.becomeArtist");
  const tErrors = useTranslations("errors");
  const create = useCreateArtist();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const [slugEdited, setSlugEdited] = useState(false);
  const schema = z.object({
    name: z.string().trim().min(1, t("nameInvalid")).max(80, t("nameInvalid")),
    slug: z
      .string()
      .trim()
      .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, t("slugInvalid"))
      .min(3, t("slugInvalid"))
      .max(40, t("slugInvalid")),
    bio: z.string().max(1000, t("bioTooLong")),
  });
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", slug: "", bio: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await create.mutateAsync({ ...values, bio: values.bio || null });
    } catch (error) {
      setFormError(applyServerErrors(error, ["name", "slug", "bio"], form.setError, (c) => tErrors(c)));
    }
  });

  const nameField = form.register("name", {
    onChange: (event: { target: { value: string } }) => {
      if (!slugEdited) form.setValue("slug", slugify(event.target.value), { shouldValidate: false });
    },
  });

  return (
    <section className="mx-auto flex max-w-xl flex-col gap-5 rounded-2xl border bg-surface p-6">
      <div className="flex items-start gap-3">
        <Mic2 className="mt-1 size-6 text-primary" aria-hidden="true" />
        <div>
          <h2 className="text-lg font-semibold">{t("title")}</h2>
          <p className="text-sm text-muted-foreground">{t("subtitle")}</p>
        </div>
      </div>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
        <TextField label={t("name")} error={form.formState.errors.name?.message} {...nameField} />
        <TextField
          label={t("slug")}
          hint={t("slugHint")}
          autoCapitalize="none"
          spellCheck={false}
          error={form.formState.errors.slug?.message}
          {...form.register("slug", { onChange: () => setSlugEdited(true) })}
        />
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="artist-bio">{t("bio")}</Label>
          <textarea
            id="artist-bio"
            rows={4}
            className="rounded-lg border border-input bg-transparent px-3 py-2 text-sm dark:bg-input/30"
            aria-invalid={form.formState.errors.bio ? true : undefined}
            {...form.register("bio")}
          />
          {form.formState.errors.bio ? (
            <p className="text-xs text-destructive">{form.formState.errors.bio.message}</p>
          ) : null}
        </div>
        <SubmitButton pending={create.isPending} className="self-start">
          {t("submit")}
        </SubmitButton>
      </form>
    </section>
  );
}
