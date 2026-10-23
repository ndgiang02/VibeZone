"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Mic2, Settings } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { Label } from "@/components/ui/label";
import { Link } from "@/i18n/navigation";
import type { ErrorCode } from "@/lib/api/errors";
import { applyServerErrors } from "@/features/auth/server-errors";
import type { Me } from "@/features/auth/api";
import { useCreateArtist } from "@/features/tracks/api";

/**
 * Turns on the artist profile. Nothing to re-enter: the profile is this account's public face — its display name,
 * username (the page URL) and picture (ADR-0013).
 */
export function BecomeArtistForm({ me }: { me: Me }) {
  const t = useTranslations("studio.becomeArtist");
  const tNav = useTranslations("nav");
  const tErrors = useTranslations("errors");
  const create = useCreateArtist();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const schema = z.object({ bio: z.string().max(1000, t("bioTooLong")) });
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { bio: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await create.mutateAsync({ bio: values.bio || null });
    } catch (error) {
      setFormError(applyServerErrors(error, ["bio"], form.setError, (c) => tErrors(c)));
    }
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
      <div className="flex items-center gap-4 rounded-xl border bg-surface-elevated p-4">
        <ArtistAvatar
          id={me.id}
          name={me.displayName || me.handle}
          src={me.avatar?.small}
          className="size-14 shrink-0 text-lg"
        />
        <div className="min-w-0 flex-1">
          <p className="truncate font-semibold">{me.displayName}</p>
          <p className="truncate text-xs text-muted-foreground">
            {t("profileUrl")}: /artists/{me.handle}
          </p>
        </div>
        <Link
          href="/settings"
          className="inline-flex h-8 shrink-0 items-center gap-1.5 rounded-full border px-3 text-xs font-medium transition-colors hover:bg-surface-hover"
        >
          <Settings className="size-3.5" aria-hidden="true" />
          {tNav("settings")}
        </Link>
      </div>
      <p className="text-xs text-muted-foreground">{t("sameAccount")}</p>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
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
