"use client";

import { useEffect, useRef } from "react";
import { notify } from "@/lib/toast";
import type { SubmissionSummary } from "@/lib/types";

// Renders nothing - just keeps a live SSE connection open for the lifetime of
// the portal layout (persists across client-side navigation) and toasts
// Approved/Rejected outcomes as they're pushed from the BFF.
export function SubmissionEvents() {
  // Dedupes on the id:status pair to avoid a duplicate toast when the same approval
  // broadcasts twice (see doc/CHRONICLE.md). A resubmission gets a new id, so this
  // never suppresses a genuinely new notification.
  const notifiedRef = useRef(new Set<string>());

  useEffect(() => {
    const source = new EventSource("/bff/api/submissions/events");

    source.onmessage = (event) => {
      const summary: SubmissionSummary = JSON.parse(event.data);
      const key = `${summary.id}:${summary.status}`;
      if (summary.status !== "Approved" && summary.status !== "Rejected") {
        return;
      }
      if (notifiedRef.current.has(key)) {
        return;
      }
      notifiedRef.current.add(key);

      if (summary.status === "Approved") {
        notify({
          title: `"${summary.titleSnapshot}" was approved`,
          description: `SKU ${summary.assignedSku}`,
        });
      } else {
        notify({
          title: `"${summary.titleSnapshot}" was rejected`,
          description: summary.rejectionReason ?? undefined,
        });
      }
    };

    return () => source.close();
  }, []);

  return null;
}
