import { render } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { SubmissionSummary } from "@/lib/types";
import { SubmissionEvents } from "./submission-events";

const { notifyMock } = vi.hoisted(() => ({ notifyMock: vi.fn() }));

vi.mock("@/lib/toast", () => ({
  notify: notifyMock,
}));

// Minimal stand-in for the browser's EventSource - just enough surface for
// the component to assign onmessage and call close().
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

function summary(overrides: Partial<SubmissionSummary>): SubmissionSummary {
  return {
    id: "sub-1",
    titleSnapshot: "Alien",
    status: "Approved",
    submittedAtUtc: "2026-01-01T00:00:00Z",
    respondedAtUtc: "2026-01-02T00:00:00Z",
    assignedSku: null,
    rejectionReason: null,
    ...overrides,
  };
}

describe("SubmissionEvents", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    notifyMock.mockClear();
    FakeEventSource.instances = [];
  });

  it("connects to the SSE endpoint and closes it on unmount", () => {
    vi.stubGlobal("EventSource", FakeEventSource);

    const { unmount } = render(<SubmissionEvents />);

    expect(FakeEventSource.instances).toHaveLength(1);
    expect(FakeEventSource.instances[0].url).toBe("/bff/api/submissions/events");

    unmount();

    expect(FakeEventSource.instances[0].close).toHaveBeenCalled();
  });

  it("notifies with the SKU on an Approved event", () => {
    vi.stubGlobal("EventSource", FakeEventSource);
    render(<SubmissionEvents />);

    const source = FakeEventSource.instances[0];
    source.onmessage?.({ data: JSON.stringify(summary({ status: "Approved", assignedSku: "SKU-123" })) });

    expect(notifyMock).toHaveBeenCalledWith({
      title: '"Alien" was approved',
      description: "SKU SKU-123",
    });
  });

  it("notifies with the rejection reason on a Rejected event", () => {
    vi.stubGlobal("EventSource", FakeEventSource);
    render(<SubmissionEvents />);

    const source = FakeEventSource.instances[0];
    source.onmessage?.({
      data: JSON.stringify(summary({ status: "Rejected", rejectionReason: "Bad description" })),
    });

    expect(notifyMock).toHaveBeenCalledWith({
      title: '"Alien" was rejected',
      description: "Bad description",
    });
  });

  it("does not notify for a Pending event", () => {
    vi.stubGlobal("EventSource", FakeEventSource);
    render(<SubmissionEvents />);

    const source = FakeEventSource.instances[0];
    source.onmessage?.({ data: JSON.stringify(summary({ status: "Pending" })) });

    expect(notifyMock).not.toHaveBeenCalled();
  });

  it("does not toast twice for the same submission's Approved status broadcast twice", () => {
    vi.stubGlobal("EventSource", FakeEventSource);
    render(<SubmissionEvents />);

    const source = FakeEventSource.instances[0];
    const approved = summary({ status: "Approved", assignedSku: "SKU-123" });
    source.onmessage?.({ data: JSON.stringify(approved) });
    source.onmessage?.({ data: JSON.stringify(approved) });

    expect(notifyMock).toHaveBeenCalledTimes(1);
  });

  it("still toasts for a different submission's Approved status after an earlier one", () => {
    vi.stubGlobal("EventSource", FakeEventSource);
    render(<SubmissionEvents />);

    const source = FakeEventSource.instances[0];
    source.onmessage?.({
      data: JSON.stringify(summary({ id: "sub-1", status: "Approved", assignedSku: "SKU-123" })),
    });
    source.onmessage?.({
      data: JSON.stringify(summary({ id: "sub-2", status: "Approved", assignedSku: "SKU-456" })),
    });

    expect(notifyMock).toHaveBeenCalledTimes(2);
  });
});
