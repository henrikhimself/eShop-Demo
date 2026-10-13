"use client";

import { useCallback, useEffect, useState } from "react";
import { PageContainer } from "@/components/page-container";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { bffFetch } from "@/lib/bff-fetch";
import { formatStatusLabel } from "@/lib/format";
import { notify } from "@/lib/toast";
import type { InventorySummary } from "@/lib/types";

function syncStatusVariant(status: InventorySummary["syncStatus"]): "default" | "destructive" | "secondary" {
  switch (status) {
    case "Confirmed":
      return "default";
    case "Failed":
      return "destructive";
    default:
      return "secondary";
  }
}

export default function InventoryPage() {
  const [items, setItems] = useState<InventorySummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [quantityDrafts, setQuantityDrafts] = useState<Record<string, string>>({});
  const [reportingSku, setReportingSku] = useState<string | null>(null);

  const reloadInventory = useCallback(async (): Promise<void> => {
    const response = await bffFetch("/bff/api/inventory");
    if (!response.ok) {
      setError("Failed to load inventory.");
      return;
    }

    const data = (await response.json()) as InventorySummary[];
    setItems(data);
    setQuantityDrafts((current) => {
      const next = { ...current };
      for (const item of data) {
        next[item.sku] ??= String(item.reportedQuantity ?? 0);
      }
      return next;
    });
  }, []);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      await reloadInventory();
      if (cancelled) {
        return;
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [reloadInventory]);

  // Refetches on any inventory event, unfiltered: quantityDrafts only fills a value
  // when absent, so a live refetch never clobbers in-progress input.
  useEffect(() => {
    const source = new EventSource("/bff/api/inventory/events");
    source.onmessage = () => void reloadInventory();

    return () => source.close();
  }, [reloadInventory]);

  async function handleReport(sku: string): Promise<void> {
    const rawQuantity = quantityDrafts[sku] ?? "0";
    const quantity = Number(rawQuantity);
    if (!Number.isInteger(quantity) || quantity < 0) {
      notify({ title: "Enter a whole number of zero or more." });
      return;
    }

    setError(null);
    setReportingSku(sku);
    try {
      const response = await bffFetch(`/bff/api/inventory/${encodeURIComponent(sku)}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ quantity }),
      });

      if (!response.ok) {
        notify({ title: `Failed to report inventory for ${sku}.` });
        return;
      }

      const updated = (await response.json()) as InventorySummary;
      setItems((current) => current?.map((item) => (item.sku === sku ? updated : item)) ?? current);
      notify({ title: `Reported ${quantity} unit(s) for ${sku}.` });
    } finally {
      setReportingSku(null);
    }
  }

  return (
    <PageContainer>
      <div>
        <h1 className="text-2xl font-semibold">Inventory</h1>
        <p className="text-sm text-muted-foreground">
          Report how many units you can supply for each of your approved SKUs.
        </p>
      </div>

      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}

      {items === null && !error && <p className="text-sm text-muted-foreground">Loading inventory…</p>}

      {items !== null && items.length === 0 && (
        <Card className="border-dashed">
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            You don&apos;t have any approved SKUs to report inventory for yet.
          </CardContent>
        </Card>
      )}

      {items !== null && items.length > 0 && (
        <Card className="overflow-hidden p-0">
          <table className="w-full border-collapse text-left text-sm">
            <thead>
              <tr className="border-b bg-muted/50 text-muted-foreground">
                <th className="px-4 py-2 font-medium">SKU</th>
                <th className="px-4 py-2 font-medium">Title</th>
                <th className="px-4 py-2 font-medium">Sync status</th>
                <th className="px-4 py-2 font-medium">Last error</th>
                <th className="px-4 py-2 font-medium">Quantity</th>
                <th className="px-4 py-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.sku} className="border-b last:border-0">
                  <td className="px-4 py-2">{item.sku}</td>
                  <td className="px-4 py-2">{item.titleSnapshot}</td>
                  <td className="px-4 py-2">
                    {item.syncStatus ? (
                      <Badge variant={syncStatusVariant(item.syncStatus)}>{formatStatusLabel(item.syncStatus)}</Badge>
                    ) : (
                      <span className="text-muted-foreground">Not reported yet</span>
                    )}
                  </td>
                  <td className="px-4 py-2">{item.lastError ?? "—"}</td>
                  <td className="px-4 py-2">
                    <Input
                      type="number"
                      min={0}
                      step={1}
                      value={quantityDrafts[item.sku] ?? ""}
                      onChange={(event) =>
                        setQuantityDrafts((current) => ({ ...current, [item.sku]: event.target.value }))
                      }
                      className="w-24"
                      aria-label={`Quantity for ${item.sku}`}
                    />
                  </td>
                  <td className="px-4 py-2">
                    <Button
                      type="button"
                      size="sm"
                      disabled={reportingSku === item.sku}
                      onClick={() => void handleReport(item.sku)}
                    >
                      Report
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </PageContainer>
  );
}
