"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { PageContainer } from "@/components/page-container";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { bffFetch } from "@/lib/bff-fetch";
import { formatStatusLabel } from "@/lib/format";
import type { DraftKind, DraftSummary } from "@/lib/types";

// Kind-specific route segment for a draft's edit page and its BFF resource.
const DRAFT_ROUTE_SEGMENT: Record<DraftKind, string> = {
  Movie: "movies",
  Merchandise: "merchandise",
};

export default function DraftsPage() {
  const router = useRouter();
  const [drafts, setDrafts] = useState<DraftSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  // Shared by the initial load and the SSE-triggered live refresh below.
  const reloadDrafts = useCallback(async (): Promise<void> => {
    const response = await bffFetch("/bff/api/drafts");
    if (!response.ok) {
      setError("Failed to load drafts.");
      return;
    }

    const data = (await response.json()) as DraftSummary[];
    setDrafts(data);
  }, []);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      await reloadDrafts();
      if (cancelled) {
        return;
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [reloadDrafts]);

  // Refetches on any submission event, unfiltered: this page has no editable form,
  // so a live refetch can't clobber in-progress input.
  useEffect(() => {
    const source = new EventSource("/bff/api/submissions/events");
    source.onmessage = () => void reloadDrafts();

    return () => source.close();
  }, [reloadDrafts]);

  async function handleNewDraft(kind: DraftKind): Promise<void> {
    setError(null);
    setCreating(true);
    try {
      const segment = DRAFT_ROUTE_SEGMENT[kind];
      const response = await bffFetch(`/bff/api/drafts/${segment}`, { method: "POST" });
      if (!response.ok) {
        setError(`Failed to create a new ${kind.toLowerCase()} draft.`);
        return;
      }

      const draft = (await response.json()) as { id: string };
      router.push(`/drafts/${segment}/${draft.id}`);
    } finally {
      setCreating(false);
    }
  }

  async function handleDelete(draft: DraftSummary): Promise<void> {
    if (!window.confirm("Delete this draft? This can't be undone.")) {
      return;
    }

    setError(null);
    setDeletingId(draft.id);
    try {
      const segment = DRAFT_ROUTE_SEGMENT[draft.kind];
      const response = await bffFetch(`/bff/api/drafts/${segment}/${draft.id}`, { method: "DELETE" });
      if (!response.ok) {
        setError("Failed to delete this draft.");
        return;
      }

      setDrafts((current) => current?.filter((d) => d.id !== draft.id) ?? current);
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <PageContainer>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Drafts</h1>
          <p className="text-sm text-muted-foreground">
            Create and edit movie and merchandise listings, then submit them for review.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button onClick={() => void handleNewDraft("Movie")} disabled={creating}>
            New movie draft
          </Button>
          <Button variant="secondary" onClick={() => void handleNewDraft("Merchandise")} disabled={creating}>
            New merchandise draft
          </Button>
        </div>
      </div>

      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}

      {drafts === null && !error && <p className="text-sm text-muted-foreground">Loading drafts…</p>}

      {drafts !== null && drafts.length === 0 && (
        <Card className="border-dashed">
          <CardContent className="flex flex-col items-center gap-2 py-10 text-center text-sm text-muted-foreground">
            <p>You don&apos;t have any drafts yet.</p>
            <p>Use &quot;New movie draft&quot; or &quot;New merchandise draft&quot; above to start one.</p>
          </CardContent>
        </Card>
      )}

      <ul className="flex flex-col gap-3">
        {drafts?.map((draft) => (
          <li key={draft.id}>
            <Card>
              <CardHeader className="flex flex-row items-center justify-between gap-3">
                <Link
                  href={`/drafts/${DRAFT_ROUTE_SEGMENT[draft.kind]}/${draft.id}`}
                  className="min-w-0 flex-1 truncate hover:underline"
                >
                  <CardTitle>{draft.title || "Untitled"}</CardTitle>
                </Link>
                <div className="flex items-center gap-2">
                  <Badge variant="outline">{draft.kind}</Badge>
                  <Badge variant={draft.status === "Draft" ? "outline" : "secondary"}>
                    {formatStatusLabel(draft.status)}
                  </Badge>
                  {draft.status === "Draft" && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => void handleDelete(draft)}
                      disabled={deletingId === draft.id}
                    >
                      Delete
                    </Button>
                  )}
                </div>
              </CardHeader>
              {draft.lastRejectionReason && (
                <CardContent>
                  <p className="text-sm text-destructive">Rejected: {draft.lastRejectionReason}</p>
                </CardContent>
              )}
            </Card>
          </li>
        ))}
      </ul>
    </PageContainer>
  );
}
