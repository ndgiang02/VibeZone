import { notFound } from "next/navigation";

/** Any unknown path under a locale renders the localized not-found page (with a 404 status). */
export default function CatchAll() {
  notFound();
}
