"use client";

import "./globals.css";

/**
 * Last-resort boundary when the root layout itself fails; it renders its own <html> and has no
 * i18n context, so the message is shown in both languages.
 */
export default function GlobalError({ retry }: { error: unknown; retry: () => void }) {
  return (
    <html lang="vi" className="dark">
      <body className="flex min-h-dvh items-center justify-center bg-background p-6 text-foreground">
        <div role="alert" className="flex max-w-md flex-col items-center gap-4 text-center">
          <h1 className="text-title font-semibold">VibeZone đang gặp sự cố</h1>
          <p className="text-muted-foreground">
            Đã có lỗi ngoài dự kiến. Vui lòng thử lại.
            <br />
            <span lang="en">Something went wrong. Please try again.</span>
          </p>
          <button
            type="button"
            onClick={retry}
            className="rounded-lg bg-primary px-4 py-2 font-medium text-primary-foreground hover:bg-primary-hover"
          >
            Thử lại · <span lang="en">Try again</span>
          </button>
        </div>
      </body>
    </html>
  );
}
