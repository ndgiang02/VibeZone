import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { jsonResponse, renderWithProviders } from "@/test/render";
import { GenreBrowser } from "./genre-browser";

function stubFetch(...responses: Array<Response | Promise<Response>>) {
  const fetchMock = vi.fn<typeof fetch>();
  for (const response of responses) {
    fetchMock.mockImplementationOnce(() => Promise.resolve(response));
  }
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

describe("GenreBrowser", () => {
  it("shows a loading skeleton while the request is pending", () => {
    stubFetch(new Promise<Response>(() => {}));

    renderWithProviders(<GenreBrowser />);

    expect(screen.getByLabelText("Loading genres")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Browse by genre" })).toHaveAttribute("aria-busy", "true");
  });

  it("renders genres returned by the API", async () => {
    const fetchMock = stubFetch(
      jsonResponse({
        data: [
          { slug: "house", name: "House" },
          { slug: "v-pop", name: "V-Pop" },
        ],
      }),
    );

    renderWithProviders(<GenreBrowser />);

    expect(await screen.findByText("V-Pop")).toBeInTheDocument();
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
    const request = fetchMock.mock.calls[0]![0] as Request;
    expect(new URL(request.url).pathname).toBe("/api/v1/genres");
  });

  it("shows an empty state when there are no genres", async () => {
    stubFetch(jsonResponse({ data: [] }));

    renderWithProviders(<GenreBrowser />);

    expect(await screen.findByText("No genres yet")).toBeInTheDocument();
  });

  it("shows an error state and recovers on retry", async () => {
    const problem = { title: "An error occurred while processing your request.", status: 500 };
    // Initial attempt + one automatic retry fail, then the manual retry succeeds.
    const fetchMock = stubFetch(
      jsonResponse(problem, 500),
      jsonResponse(problem, 500),
      jsonResponse({ data: [{ slug: "jazz", name: "Jazz" }] }),
    );

    renderWithProviders(<GenreBrowser />);

    expect(await screen.findByRole("alert")).toHaveTextContent("Couldn't load genres");
    await userEvent.click(screen.getByRole("button", { name: "Try again" }));

    expect(await screen.findByText("Jazz")).toBeInTheDocument();
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
  });

  it("does not retry client errors", async () => {
    const fetchMock = stubFetch(jsonResponse({ title: "Not Found", status: 404 }, 404));

    renderWithProviders(<GenreBrowser />);

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
