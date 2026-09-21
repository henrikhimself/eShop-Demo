import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import SubmissionsPage from "./page";

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

describe("SubmissionsPage", () => {
  beforeEach(() => {
    FakeEventSource.instances = [];
    vi.stubGlobal("EventSource", FakeEventSource);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders an empty state when there are no submissions", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify([]), { status: 200 })));

    render(<SubmissionsPage />);

    expect(await screen.findByText(/haven.t submitted any drafts for review yet/i)).toBeInTheDocument();
  });

  it("renders a row per submission with status, SKU, and rejection reason", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify([
            {
              id: "1",
              titleSnapshot: "Alien",
              status: "Approved",
              submittedAtUtc: "2026-01-01T00:00:00Z",
              respondedAtUtc: "2026-01-02T00:00:00Z",
              assignedSku: "SKU-123",
              rejectionReason: null,
            },
            {
              id: "2",
              titleSnapshot: "Predator",
              status: "Rejected",
              submittedAtUtc: "2026-01-03T00:00:00Z",
              respondedAtUtc: "2026-01-04T00:00:00Z",
              assignedSku: null,
              rejectionReason: "Poor image quality",
            },
          ]),
          { status: 200 },
        ),
      ),
    );

    render(<SubmissionsPage />);

    expect(await screen.findByText("Alien")).toBeInTheDocument();
    expect(screen.getByText("SKU-123")).toBeInTheDocument();
    expect(screen.getByText("Predator")).toBeInTheDocument();
    expect(screen.getByText("Poor image quality")).toBeInTheDocument();
  });

  it("refetches the whole list when an SSE message arrives", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify([]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    render(<SubmissionsPage />);

    await screen.findByText(/haven.t submitted any drafts for review yet/i);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(FakeEventSource.instances).toHaveLength(1);

    FakeEventSource.instances[0]?.onmessage?.({ data: "{}" });

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
  });
});
