"use client";

import { useCallback, useEffect, useState } from "react";
import { PageContainer } from "@/components/page-container";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { bffFetch } from "@/lib/bff-fetch";
import { formatStatusLabel } from "@/lib/format";
import type { SubmissionSummary } from "@/lib/types";

function statusVariant(status: SubmissionSummary["status"]): "default" | "destructive" | "secondary" {
  switch (status) {
    case "Approved":
      return "default";
    case "Rejected":
      return "destructive";
    default:
      return "secondary";
  }
}

export default function SubmissionsPage() {
  const [submissions, setSubmissions] = useState<SubmissionSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reloadSubmissions = useCallback(async (): Promise<void> => {
    const response = await bffFetch("/bff/api/submissions");
    if (!response.ok) {
      setError("Failed to load submissions.");
      return;
    }

    const data = (await response.json()) as SubmissionSummary[];
    setSubmissions(data);
  }, []);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      await reloadSubmissions();
      if (cancelled) {
        return;
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [reloadSubmissions]);

  // Refetches on any submission event, unfiltered: this page has no editable form,
  // so a live refetch can't clobber in-progress input.
  useEffect(() => {
    const source = new EventSource("/bff/api/submissions/events");
    source.onmessage = () => void reloadSubmissions();

    return () => source.close();
  }, [reloadSubmissions]);

  return (
    <PageContainer>
      <div>
        <h1 className="text-2xl font-semibold">Submission history</h1>
        <p className="text-sm text-muted-foreground">
          Every draft you have submitted for review, and the Merchandiser&apos;s decision.
        </p>
      </div>

      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}

      {submissions === null && !error && <p className="text-sm text-muted-foreground">Loading submissions…</p>}

      {submissions !== null && submissions.length === 0 && (
        <Card className="border-dashed">
          <CardContent className="py-10 text-center text-sm text-muted-foreground">
            You haven&apos;t submitted any drafts for review yet.
          </CardContent>
        </Card>
      )}

      {submissions !== null && submissions.length > 0 && (
        <Card className="overflow-hidden p-0">
          <table className="w-full border-collapse text-left text-sm">
            <thead>
              <tr className="border-b bg-muted/50 text-muted-foreground">
                <th className="px-4 py-2 font-medium">Title</th>
                <th className="px-4 py-2 font-medium">Status</th>
                <th className="px-4 py-2 font-medium">Submitted</th>
                <th className="px-4 py-2 font-medium">Responded</th>
                <th className="px-4 py-2 font-medium">Assigned SKU</th>
                <th className="px-4 py-2 font-medium">Rejection reason</th>
              </tr>
            </thead>
            <tbody>
              {submissions.map((submission) => (
                <tr key={submission.id} className="border-b last:border-0">
                  <td className="px-4 py-2">{submission.titleSnapshot}</td>
                  <td className="px-4 py-2">
                    <Badge variant={statusVariant(submission.status)}>{formatStatusLabel(submission.status)}</Badge>
                  </td>
                  <td className="px-4 py-2">{new Date(submission.submittedAtUtc).toLocaleString()}</td>
                  <td className="px-4 py-2">
                    {submission.respondedAtUtc ? new Date(submission.respondedAtUtc).toLocaleString() : "—"}
                  </td>
                  <td className="px-4 py-2">{submission.assignedSku ?? "—"}</td>
                  <td className="px-4 py-2">{submission.rejectionReason ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </PageContainer>
  );
}
