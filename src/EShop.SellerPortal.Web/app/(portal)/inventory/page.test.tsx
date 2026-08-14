import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import InventoryPage from "./page";

const { notifyMock } = vi.hoisted(() => ({ notifyMock: vi.fn() }));

vi.mock("@/lib/toast", () => ({
  notify: notifyMock,
}));

// Minimal stand-in for the browser's EventSource - see submissions/page.test.tsx for
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

describe("InventoryPage", () => {
  beforeEach(() => {
    notifyMock.mockClear();
    document.cookie = "XSRF-TOKEN=test-token; path=/";
    FakeEventSource.instances = [];
    vi.stubGlobal("EventSource", FakeEventSource);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearCookies();
  });

  it("renders an empty state when there are no approved SKUs", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify([]), { status: 200 })));

    render(<InventoryPage />);

    expect(await screen.findByText(/don.t have any approved SKUs/i)).toBeInTheDocument();
  });

  it("renders a row per approved SKU with its sync status and last error", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify([
            {
              sku: "SKU-1",
              titleSnapshot: "Alien",
              reportedQuantity: 5,
              syncStatus: "Confirmed",
              lastSyncedAtUtc: "2026-01-01T00:00:00Z",
              lastError: null,
            },
            {
              sku: "SKU-2",
              titleSnapshot: "Predator",
              reportedQuantity: 2,
              syncStatus: "Failed",
              lastSyncedAtUtc: "2026-01-02T00:00:00Z",
              lastError: "Warehouse unreachable",
            },
            {
              sku: "SKU-3",
              titleSnapshot: "Never Reported",
              reportedQuantity: null,
              syncStatus: null,
              lastSyncedAtUtc: null,
              lastError: null,
            },
          ]),
          { status: 200 },
        ),
      ),
    );

    render(<InventoryPage />);

    expect(await screen.findByText("Alien")).toBeInTheDocument();
    expect(screen.getByText("Predator")).toBeInTheDocument();
    expect(screen.getByText("Warehouse unreachable")).toBeInTheDocument();
    expect(screen.getByText("Never Reported")).toBeInTheDocument();
    expect(screen.getByText(/not reported yet/i)).toBeInTheDocument();
  });

  it("reports a quantity for a SKU and shows the updated sync status", async () => {
    const fetchMock = vi.fn(async (input: string, init?: RequestInit) => {
      if (input === "/bff/api/inventory" && (!init || init.method === undefined)) {
        return new Response(
          JSON.stringify([
            {
              sku: "SKU-1",
              titleSnapshot: "Alien",
              reportedQuantity: null,
              syncStatus: null,
              lastSyncedAtUtc: null,
              lastError: null,
            },
          ]),
          { status: 200 },
        );
      }
      if (input === "/bff/api/inventory/SKU-1" && init?.method === "POST") {
        return new Response(
          JSON.stringify({
            sku: "SKU-1",
            titleSnapshot: "Alien",
            reportedQuantity: 8,
            syncStatus: "Pending",
            lastSyncedAtUtc: null,
            lastError: null,
          }),
          { status: 200 },
        );
      }
      throw new Error(`Unexpected fetch call: ${input}`);
    });
    vi.stubGlobal("fetch", fetchMock);

    render(<InventoryPage />);

    await screen.findByText("Alien");

    fireEvent.change(screen.getByLabelText("Quantity for SKU-1"), { target: { value: "8" } });
    fireEvent.click(screen.getByRole("button", { name: "Report" }));

    await waitFor(() => expect(notifyMock).toHaveBeenCalledWith({ title: "Reported 8 unit(s) for SKU-1." }));
    expect(await screen.findByText("Pending")).toBeInTheDocument();
  });

  it("refetches the whole list when an SSE message arrives", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify([]), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    render(<InventoryPage />);

    await screen.findByText(/don.t have any approved SKUs/i);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(FakeEventSource.instances).toHaveLength(1);

    FakeEventSource.instances[0]?.onmessage?.({ data: "{}" });

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
  });
});
