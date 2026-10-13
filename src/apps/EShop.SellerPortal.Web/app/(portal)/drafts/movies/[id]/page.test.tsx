import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { Suspense } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { MovieDraftDetail } from "@/lib/types";
import EditMovieDraftPage from "./page";

const pushMock = vi.fn();
const { notifyMock } = vi.hoisted(() => ({ notifyMock: vi.fn() }));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: pushMock }),
}));

vi.mock("@/lib/toast", () => ({
  notify: notifyMock,
}));

// Minimal stand-in for the browser's EventSource - see submission-events.test.tsx for
// the same shape. Needed here because the PendingReview/PendingImageCleanup effect
// opens one.
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

// `use(params)` suspends on mount; the initial render must be awaited inside `act`
// so that the promise's resolution and the resulting re-render are flushed before
// the test makes assertions.
async function renderPage(id = "draft-1"): Promise<void> {
  await act(async () => {
    render(
      <Suspense fallback={<div>loading route</div>}>
        <EditMovieDraftPage params={Promise.resolve({ id })} />
      </Suspense>,
    );
  });
}

function baseDraft(overrides: Partial<MovieDraftDetail> = {}): MovieDraftDetail {
  return {
    id: "draft-1",
    status: "Draft",
    title: "Alien",
    genre: "Horror",
    description: "A crew in deep space.",
    yearOfRelease: 1979,
    lastRejectionReason: null,
    formatVariants: [],
    coverImage: null,
    ...overrides,
  };
}

describe("EditMovieDraftPage", () => {
  beforeEach(() => {
    pushMock.mockClear();
    notifyMock.mockClear();
    document.cookie = "XSRF-TOKEN=test-token; path=/";
    FakeEventSource.instances = [];
    vi.stubGlobal("EventSource", FakeEventSource);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearCookies();
  });

  it("renders the draft's fields and a rejection alert when present", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify(
            baseDraft({
              lastRejectionReason: "Description too short",
              formatVariants: [{ id: "v1", format: "Dvd", price: 9.99 }],
            }),
          ),
          { status: 200 },
        ),
      ),
    );

    await renderPage();

    expect(await screen.findByDisplayValue("Alien")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Horror")).toBeInTheDocument();
    expect(screen.getByText("Description too short")).toBeInTheDocument();
  });

  it("disables Submit for review until there is one cover image and at least one format variant", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify(baseDraft()), { status: 200 })),
    );

    await renderPage();

    const submitButton = await screen.findByRole("button", { name: "Submit for review" });
    expect(submitButton).toBeDisabled();
    expect(
      screen.getByText("Add exactly one cover image and at least one format variant before submitting."),
    ).toBeInTheDocument();
  });

  it("enables Submit for review once a cover image and a format variant exist", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify(
            baseDraft({
              formatVariants: [{ id: "v1", format: "Dvd", price: 9.99 }],
              coverImage: { id: "img-1", url: "/bff/api/drafts/movies/draft-1/images/img-1" },
            }),
          ),
          { status: 200 },
        ),
      ),
    );

    await renderPage();

    const submitButton = await screen.findByRole("button", { name: "Submit for review" });
    expect(submitButton).toBeEnabled();
  });

  it("saves scalar field and format variant changes via PUT", async () => {
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/drafts/movies/draft-1" && init?.method === undefined) {
        return new Response(JSON.stringify(baseDraft()), { status: 200 });
      }
      if (input === "/bff/api/drafts/movies/draft-1" && init?.method === "PUT") {
        const body = JSON.parse(init.body as string);
        return new Response(JSON.stringify(baseDraft(body)), { status: 200 });
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    await renderPage();

    const titleInput = await screen.findByDisplayValue("Alien");
    fireEvent.change(titleInput, { target: { value: "Aliens" } });

    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        "/bff/api/drafts/movies/draft-1",
        expect.objectContaining({ method: "PUT" }),
      ),
    );

    const putCall = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT")!;
    const body = JSON.parse((putCall[1] as RequestInit).body as string);
    expect(body.title).toBe("Aliens");
    expect(notifyMock).toHaveBeenCalledWith({ title: "Draft saved" });
  });

  it("shows the uploaded cover image without losing the rest of the draft", async () => {
    // The upload endpoint returns only a DraftImageDto, so the page must merge the new
    // image into the existing draft state.
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/drafts/movies/draft-1" && init?.method === undefined) {
        return new Response(
          JSON.stringify(baseDraft({ formatVariants: [{ id: "v1", format: "Dvd", price: 9.99 }] })),
          { status: 200 },
        );
      }
      if (input === "/bff/api/drafts/movies/draft-1/images" && init?.method === "POST") {
        return new Response(
          JSON.stringify({ id: "img-1", url: "/bff/api/drafts/movies/draft-1/images/img-1" }),
          { status: 200 },
        );
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    await renderPage();

    const fileInput = document.querySelector("input[type='file']") as HTMLInputElement;
    const file = new File(["cover"], "cover.png", { type: "image/png" });
    await act(async () => {
      fireEvent.change(fileInput, { target: { files: [file] } });
    });

    expect(await screen.findByRole("button", { name: "Remove cover image" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Submit for review" })).toBeEnabled();
  });

  it("disables the fields after submitting for review without losing the rest of the draft", async () => {
    // Submit for review now auto-saves first (a PUT), then submits (a POST) - the
    // page must keep the existing draft detail and only update the local status.
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/drafts/movies/draft-1" && init?.method === undefined) {
        return new Response(
          JSON.stringify(
            baseDraft({
              formatVariants: [{ id: "v1", format: "Dvd", price: 9.99 }],
              coverImage: { id: "img-1", url: "/bff/api/drafts/movies/draft-1/images/img-1" },
            }),
          ),
          { status: 200 },
        );
      }
      if (input === "/bff/api/drafts/movies/draft-1" && init?.method === "PUT") {
        const body = JSON.parse(init.body as string);
        return new Response(
          JSON.stringify(
            baseDraft({
              ...body,
              formatVariants: [{ id: "v1", format: "Dvd", price: 9.99 }],
              coverImage: { id: "img-1", url: "/bff/api/drafts/movies/draft-1/images/img-1" },
            }),
          ),
          { status: 200 },
        );
      }
      if (input === "/bff/api/drafts/movies/draft-1/submit" && init?.method === "POST") {
        return new Response(
          JSON.stringify({
            id: "sub-1",
            titleSnapshot: "Alien",
            status: "Pending",
            submittedAtUtc: "2026-01-01T00:00:00Z",
            respondedAtUtc: null,
            assignedSku: null,
            rejectionReason: null,
          }),
          { status: 200 },
        );
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    await renderPage();

    const submitButton = await screen.findByRole("button", { name: "Submit for review" });
    expect(submitButton).toBeEnabled();
    // Awaited inside act() so the PendingReview-triggered SSE effect finishes mounting
    // before this test ends and afterEach() tears down the EventSource stub.
    await act(async () => {
      fireEvent.click(submitButton);
    });

    expect(await screen.findByDisplayValue("Alien")).toBeDisabled();
    expect(notifyMock).toHaveBeenCalledWith({ title: "Draft saved and submitted for review" });
  });

  it("deletes the draft and navigates back to /drafts on confirm", async () => {
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/drafts/movies/draft-1" && init?.method === undefined) {
        return new Response(JSON.stringify(baseDraft()), { status: 200 });
      }
      if (input === "/bff/api/drafts/movies/draft-1" && init?.method === "DELETE") {
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("confirm", vi.fn().mockReturnValue(true));

    await renderPage();

    const deleteButton = await screen.findByRole("button", { name: "Delete draft" });
    expect(deleteButton).toBeEnabled();
    fireEvent.click(deleteButton);

    await waitFor(() => expect(pushMock).toHaveBeenCalledWith("/drafts"));
  });

  it("disables Delete draft once the draft leaves Draft status", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify(baseDraft({ status: "PendingReview" })), { status: 200 }),
      ),
    );

    await renderPage();

    expect(await screen.findByRole("button", { name: "Delete draft" })).toBeDisabled();
  });

  it("shows no Cancel review button while the draft is in Draft status", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify(baseDraft()), { status: 200 })));

    await renderPage();

    await screen.findByDisplayValue("Alien");
    expect(screen.queryByRole("button", { name: "Cancel review" })).not.toBeInTheDocument();
  });

  it("cancels the review and reverts to the editable Draft view", async () => {
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/drafts/movies/draft-1" && init?.method === undefined) {
        return new Response(JSON.stringify(baseDraft({ status: "PendingReview" })), { status: 200 });
      }
      if (input === "/bff/api/drafts/movies/draft-1/cancel-review" && init?.method === "POST") {
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    await renderPage();

    const cancelButton = await screen.findByRole("button", { name: "Cancel review" });
    fireEvent.click(cancelButton);

    await waitFor(() => expect(notifyMock).toHaveBeenCalledWith({ title: "Review cancelled" }));
    expect(await screen.findByDisplayValue("Alien")).toBeEnabled();
    expect(screen.queryByRole("button", { name: "Cancel review" })).not.toBeInTheDocument();
  });

  it("keeps listening through PendingImageCleanup and shows the Approved panel once the draft is removed", async () => {
    let draftRemoved = false;
    const fetchMock = vi.fn(async (input: string) => {
      if (input === "/bff/api/drafts/movies/draft-1") {
        if (draftRemoved) {
          return new Response(null, { status: 404 });
        }
        return new Response(JSON.stringify(baseDraft({ status: "PendingImageCleanup" })), { status: 200 });
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    await renderPage();

    await screen.findByDisplayValue("Alien");
    expect(FakeEventSource.instances).toHaveLength(1);

    draftRemoved = true;
    await act(async () => {
      FakeEventSource.instances[0]?.onmessage?.({ data: "{}" });
    });

    expect(await screen.findByText("Approved")).toBeInTheDocument();
  });
});
