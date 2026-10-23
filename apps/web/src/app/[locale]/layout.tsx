import type { Metadata, Viewport } from "next";
import { hasLocale, NextIntlClientProvider } from "next-intl";
import { getTranslations } from "next-intl/server";
import { Geist, Geist_Mono } from "next/font/google";
import { headers } from "next/headers";
import { notFound } from "next/navigation";
import { AppShell } from "@/components/shell/app-shell";
import { routing } from "@/i18n/routing";
import { CspNonce } from "../csp-nonce";
import { Providers } from "../providers";
import "../globals.css";

const geistSans = Geist({ variable: "--font-geist-sans", subsets: ["latin", "latin-ext", "vietnamese"] });
const geistMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] });

export async function generateMetadata({ params }: LayoutProps<"/[locale]">): Promise<Metadata> {
  const { locale } = await params;
  const t = await getTranslations({
    locale: hasLocale(routing.locales, locale) ? locale : routing.defaultLocale,
  });
  return {
    title: { default: "VibeZone", template: "%s · VibeZone" },
    description: t("metadata.description"),
  };
}

export const viewport: Viewport = {
  themeColor: "#050505",
  colorScheme: "dark",
};

export default async function LocaleLayout({ children, params }: LayoutProps<"/[locale]">) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) {
    notFound();
  }

  // Reading request headers renders every page per request, which is what lets Next.js stamp the
  // per-request CSP nonce (set in src/proxy.ts) onto its scripts (ADR-0006).
  const nonce = (await headers()).get("x-nonce") ?? undefined;

  return (
    <html lang={locale} className={`dark ${geistSans.variable} ${geistMono.variable}`}>
      <body className="min-h-dvh">
        <CspNonce nonce={nonce} />
        <NextIntlClientProvider>
          <Providers>
            <AppShell>{children}</AppShell>
          </Providers>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
