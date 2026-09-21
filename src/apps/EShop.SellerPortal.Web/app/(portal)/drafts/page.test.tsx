import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import DraftsPage from "./page";

const pushMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
}));

// Minimal stand-in for the browser's EventSource - see submission-events.test.tsx for
// the same shape. Needed here because the page opens one unconditionally on mount.
class FakeEventSource {
  url: string;
  onmessage: ((event: { data: string }) => void) | null = null;
  close = vi.fn();

  constructor(url: string) {
    this.url = url;
    FakeEventSource.instances.push(this);
  }

  static instances: FakeEventSource[] = [];
}

function clearCookies(): void {
  for (const entry of document.cookie.split(";")) {
    const name = entry.split("=")[0]?.trim();
    if (name) {
      document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
    }
  }
}

describe("DraftsPage", () => {
  beforeEach(() => {
    pushMock.mockClear();
    document.cookie = "XSRF-TOKEN=test-token; path=/";
    FakeEventSource.instances = [];
    vi.stubGlobal("EventSource", FakeEventSource);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearCookies();
  });

  it("renders drafts with a status badge, a kind badge, and a rejection reason", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify([
            { id: "1", status: "Draft", kind: "Movie", title: "Alien", lastRejectionReason: null },
            {
              id: "2",
              status: "PendingReview",
              kind: "Merchandise",
              title: "Alien Figure",
              lastRejectionReason: "Bad description",
            },
          ]),
          { status: 200 },
        ),
      ),
    );

    render(<DraftsPage />);

    expect(await screen.findByText("Alien")).toBeInTheDocument();
    expect(screen.getByText("Alien Figure")).toBeInTheDocument();
    expect(screen.getByText("Movie")).toBeInTheDocument();
    expect(screen.getByText("Merchandise")).toBeInTheDocument();
    expect(screen.getByText("Pending review")).toBeInTheDocument();
    expect(screen.getByText("Rejected: Bad description")).toBeInTheDocument();
  });

  it("creates a new movie draft and navigates to its edit page", async () => {
    const fetchMock = vi.fn(async (input: string) => {
      if (input === "/bff/api/drafts") {
        return new Response(JSON.stringify([]), { status: 200 });
      }
      if (input === "/bff/api/drafts/movies") {
        return new Response(JSON.stringify({ id: "new-id" }), { status: 201 });
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    render(<DraftsPage />);

    await screen.findByText(/don.t have any drafts yet/i);

    fireEvent.click(screen.getByRole("button", { name: "New movie draft" }));

    await waitFor(() => expect(pushMock).toHaveBeenCalledWith("/drafts/movies/new-id"));
  });

  it("creates a new merchandise draft and navigates to its edit page", async () => {
    const fetchMock = vi.fn(async (input: string) => {
      if (input === "/bff/api/drafts") {
        return new Response(JSON.stringify([]), { status: 200 });
      }
      if (input === "/bff/api/drafts/merchandise") {
        return new Response(JSON.stringify({ id: "new-merch-id" }), { status: 201 });
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    render(<DraftsPage />);

    await screen.findByText(/don.t have any drafts yet/i);

    fireEvent.click(screen.getByRole("button", { name: "New merchandise draft" }));

    await waitFor(() => expect(pushMock).toHaveBeenCalledWith("/drafts/merchandise/new-merch-id"));
  });

  it("links each draft to its kind-specific edit page", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify([
            { id: "1", status: "Draft", kind: "Movie", title: "Alien", lastRejectionReason: null },
            { id: "2", status: "Draft", kind: "Merchandise", title: "Alien Figure", lastRejectionReason: null },
          ]),
          { status: 200 },
        ),
      ),
    );

    render(<DraftsPage />);

    expect(await screen.findByRole("link", { name: "Alien" })).toHaveAttribute("href", "/drafts/movies/1");
    expect(screen.getByRole("link", { name: "Alien Figure" })).toHaveAttribute(
      "href",
      "/drafts/merchandise/2",
    );
  });

  it("shows Delete only for Draft-status drafts, and removes the draft from the list on confirm", async () => {
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/drafts" && init?.method === undefined) {
        return new Response(
          JSON.stringify([
            { id: "1", status: "Draft", kind: "Movie", title: "Alien", lastRejectionReason: null },
            { id: "2", status: "PendingReview", kind: "Movie", title: "Predator", lastRejectionReason: null },
          ]),
          { status: 200 },
        );
      }
      if (input === "/bff/api/drafts/movies/1" && init?.method === "DELETE") {
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(true));

    render(<DraftsPage />);

    await screen.findByText("Alien");
    const deleteButtons = screen.getAllByRole("button", { name: "Delete" });
    expect(deleteButtons).toHaveLength(1);

    fireEvent.click(deleteButtons[0]);

    await waitFor(() => expect(screen.queryByText("Alien")).not.toBeInTheDocument());
    expect(screen.getByText("Predator")).toBeInTheDocument();
  });

  it("deletes a merchandise draft via the merchandise BFF route", async () => {
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/drafts" && init?.method === undefined) {
        return new Response(
          JSON.stringify([
            { id: "1", status: "Draft", kind: "Merchandise", title: "Alien Figure", lastRejectionReason: null },
          ]),
          { status: 200 },
        );
      }
      if (input === "/bff/api/drafts/merchandise/1" && init?.method === "DELETE") {
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(true));

    render(<DraftsPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Delete" }));

    await waitFor(() => expect(screen.queryByText("Alien Figure")).not.toBeInTheDocument());
    expect(fetchMock).toHaveBeenCalledWith(
      "/bff/api/drafts/merchandise/1",
      expect.objectContaining({ method: "DELETE" }),
    );
  });

  it("refetches the whole list when an SSE message arrives", async () => {
    const fetchMock = vi.fn(async (input: string) => {
      if (input === "/bff/api/drafts") {
        return new Response(
          JSON.stringify([
            { id: "1", status: "Draft", kind: "Movie", title: "Alien", lastRejectionReason: null },
          ]),
          { status: 200 },
        );
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    render(<DraftsPage />);

    await screen.findByText("Alien");
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(FakeEventSource.instances).toHaveLength(1);

    FakeEventSource.instances[0]?.onmessage?.({ data: "{}" });

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
  });

  it("does not delete when the confirmation is declined", async () => {
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/drafts" && init?.method === undefined) {
        return new Response(
          JSON.stringify([
            { id: "1", status: "Draft", kind: "Movie", title: "Alien", lastRejectionReason: null },
          ]),
          { status: 200 },
        );
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(false));

    render(<DraftsPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Delete" }));

    expect(screen.getByText("Alien")).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalledWith(
      "/bff/api/drafts/movies/1",
      expect.objectContaining({ method: "DELETE" }),
    );
  });
});
