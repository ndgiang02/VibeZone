import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { jsonResponse, renderWithProviders } from "@/test/render";
import { titleFromFileName, UploadTrackForm } from "./upload-track-form";

type Sent = { method: string; url: string; headers: Record<string, string>; body: unknown };

/** Minimal XHR double: records the request, reports progress, then answers with `respond()`. */
class FakeXhr {
  static sent: Sent[] = [];
  static respond: () => { status: number; body: unknown } = () => ({ status: 202, body: {} });

  upload: {
    onprogress: ((event: { lengthComputable: boolean; loaded: number; total: number }) => void) | null;
  } = {
    onprogress: null,
  };
  status = 0;
  responseText = "";
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onabort: (() => void) | null = null;
  private request: Sent = { method: "", url: "", headers: {}, body: null };

  open(method: string, url: string) {
    this.request = { ...this.request, method, url };
  }

  setRequestHeader(name: string, value: string) {
    this.request.headers[name] = value;
  }

  send(body: unknown) {
    FakeXhr.sent.push({ ...this.request, body });
    setTimeout(() => {
      this.upload.onprogress?.({ lengthComputable: true, loaded: 50, total: 100 });
      const { status, body: response } = FakeXhr.respond();
      this.status = status;
      this.responseText = JSON.stringify(response);
      this.onload?.();
    }, 0);
  }

  abort() {
    this.onabort?.();
  }
}

beforeEach(() => {
  FakeXhr.sent = [];
  vi.stubGlobal("XMLHttpRequest", FakeXhr);
  vi.stubGlobal(
    "fetch",
    vi.fn<typeof fetch>(async (input) => {
      const path = new URL((input as Request).url).pathname;
      if (path === "/api/v1/auth/csrf") return jsonResponse({ data: { token: "csrf-token" } });
      return jsonResponse({ data: [{ slug: "pop", name: "Pop" }] });
    }),
  );
  // jsdom has no blob URLs.
  URL.createObjectURL = vi.fn(() => "blob:preview");
  URL.revokeObjectURL = vi.fn();
});

const audioFile = () => new File(["ID3 fake mp3"], "My_Song (Final).mp3", { type: "audio/mpeg" });
const coverFile = () => new File(["png"], "cover.png", { type: "image/png" });

describe("UploadTrackForm", () => {
  it("sends the details, cover and audio in one multipart request", async () => {
    FakeXhr.respond = () => ({ status: 202, body: { data: { trackId: "t-1", status: "Uploaded" } } });
    const onCreated = vi.fn();
    renderWithProviders(<UploadTrackForm onCreated={onCreated} />);

    await userEvent.upload(screen.getByLabelText(/Choose an audio file/), audioFile());
    await userEvent.upload(screen.getByLabelText(/Add cover art/), coverFile());
    expect(screen.getByLabelText("Title")).toHaveValue("My Song (Final)");
    await userEvent.selectOptions(await screen.findByLabelText("Genre"), "pop");
    await userEvent.selectOptions(screen.getByLabelText("Visibility"), "Unlisted");
    await userEvent.click(screen.getByRole("button", { name: "Upload" }));

    expect(await screen.findByText("Uploaded!")).toBeInTheDocument();
    expect(onCreated).toHaveBeenCalledWith("t-1");
    expect(FakeXhr.sent).toHaveLength(1);
    const [request] = FakeXhr.sent;
    expect(request).toMatchObject({
      method: "POST",
      url: "/api/v1/track-uploads",
      headers: { "X-CSRF-TOKEN": "csrf-token" },
    });
    const form = request!.body as FormData;
    expect(form.get("title")).toBe("My Song (Final)");
    expect(form.get("visibility")).toBe("Unlisted");
    expect(form.get("genreSlug")).toBe("pop");
    expect(form.get("description")).toBeNull();
    expect((form.get("artwork") as File).name).toBe("cover.png");
    expect((form.get("audio") as File).name).toBe("My_Song (Final).mp3");
    // Text fields go first, the large file last.
    expect([...form.keys()]).toEqual(["title", "visibility", "genreSlug", "artwork", "audio"]);
  });

  it("requires an audio file before sending anything", async () => {
    renderWithProviders(<UploadTrackForm onCreated={vi.fn()} />);

    await userEvent.type(screen.getByLabelText("Title"), "No audio");
    await userEvent.click(screen.getByRole("button", { name: "Upload" }));

    expect(await screen.findByText("Choose an audio file.")).toBeInTheDocument();
    expect(FakeXhr.sent).toHaveLength(0);
  });

  it("rejects covers that aren't JPEG, PNG or WebP without uploading", async () => {
    const user = userEvent.setup({ applyAccept: false });
    renderWithProviders(<UploadTrackForm onCreated={vi.fn()} />);

    await user.upload(
      screen.getByLabelText(/Add cover art/),
      new File(["<svg/>"], "x.svg", { type: "image/svg+xml" }),
    );

    expect(screen.getByRole("alert")).toHaveTextContent("Use a JPEG, PNG or WebP image.");
    expect(URL.createObjectURL).not.toHaveBeenCalled();
  });

  it("shows a server error on the cover and keeps the form filled in", async () => {
    FakeXhr.respond = () => ({
      status: 400,
      body: { status: 400, code: "artwork_too_small", errors: { artwork: ["artwork_too_small"] } },
    });
    renderWithProviders(<UploadTrackForm onCreated={vi.fn()} />);

    await userEvent.upload(screen.getByLabelText(/Choose an audio file/), audioFile());
    await userEvent.upload(screen.getByLabelText(/Add cover art/), coverFile());
    await userEvent.click(screen.getByRole("button", { name: "Upload" }));

    expect(await screen.findByText("Cover art must be at least 200 × 200 px.")).toBeInTheDocument();
    expect(screen.getByLabelText("Title")).toHaveValue("My Song (Final)");
    expect(screen.getByText("My_Song (Final).mp3")).toBeInTheDocument();
  });
});

describe("titleFromFileName", () => {
  it("drops the extension and underscores", () => {
    expect(titleFromFileName("01_Intro__v2.final.wav")).toBe("01 Intro v2.final");
  });
});
