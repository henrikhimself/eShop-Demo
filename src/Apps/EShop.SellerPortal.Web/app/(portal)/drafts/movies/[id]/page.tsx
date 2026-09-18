"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { use, useCallback, useEffect, useState } from "react";
import { PageContainer } from "@/components/page-container";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { bffFetch } from "@/lib/bff-fetch";
import { formatStatusLabel } from "@/lib/format";
import { notify } from "@/lib/toast";
import { MOVIE_FORMATS, type DraftImageDto, type MovieDraftDetail, type MovieFormat } from "@/lib/types";

interface EditableVariant {
  key: string;
  id: string | null;
  format: MovieFormat;
  price: string;
}

function toEditableVariants(detail: MovieDraftDetail): EditableVariant[] {
  return detail.formatVariants.map((variant) => ({
    key: variant.id,
    id: variant.id,
    format: variant.format,
    price: String(variant.price),
  }));
}

export default function EditMovieDraftPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();

  const [detail, setDetail] = useState<MovieDraftDetail | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [approvedAndRemoved, setApprovedAndRemoved] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const [title, setTitle] = useState("");
  const [genre, setGenre] = useState("");
  const [description, setDescription] = useState("");
  const [yearOfRelease, setYearOfRelease] = useState(new Date().getFullYear());
  const [variants, setVariants] = useState<EditableVariant[]>([]);

  const [saving, setSaving] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [deleting, setDeleting] = useState(false);

  // Shared by the initial load and the SSE-triggered live refresh below: fetches this
  // draft and applies it to state, or records why that failed.
  const refetchDraft = useCallback(async (): Promise<void> => {
    const response = await bffFetch(`/bff/api/drafts/movies/${id}`);
    if (!response.ok) {
      if (response.status === 404) {
        // The Merchandiser approved this draft and its image cleanup completed, so
        // SubmissionResultConsumer removed the row - an expected terminal outcome,
        // not an unexpected failure.
        setApprovedAndRemoved(true);
      } else {
        setLoadError("Failed to load this draft.");
      }
      return;
    }

    const data = (await response.json()) as MovieDraftDetail;
    setDetail(data);
    setTitle(data.title);
    setGenre(data.genre);
    setDescription(data.description);
    setYearOfRelease(Number(data.yearOfRelease));
    setVariants(toEditableVariants(data));
  }, [id]);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      await refetchDraft();
      if (cancelled) {
        return;
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [refetchDraft]);

  // Refetches on any submission event, unfiltered: a Seller has at most one Pending
  // submission per draft, and fields are disabled while Pending, so this can't clobber
  // an edit. Stays open through PendingImageCleanup too, so a 404 here can show the
  // "Approved" panel once SubmissionImageDeletionConsumer removes the draft.
  useEffect(() => {
    if (detail?.status !== "PendingReview" && detail?.status !== "PendingImageCleanup") {
      return;
    }

    const source = new EventSource("/bff/api/submissions/events");
    source.onmessage = () => void refetchDraft();

    return () => source.close();
  }, [detail?.status, refetchDraft]);

  function addVariant(): void {
    setVariants((current) => [
      ...current,
      { key: crypto.randomUUID(), id: null, format: "Dvd", price: "0" },
    ]);
  }

  function removeVariant(key: string): void {
    setVariants((current) => current.filter((variant) => variant.key !== key));
  }

  function updateVariant(key: string, changes: Partial<Pick<EditableVariant, "format" | "price">>): void {
    setVariants((current) =>
      current.map((variant) => (variant.key === key ? { ...variant, ...changes } : variant)),
    );
  }

  // Shared by "Save changes" and the auto-save step of "Submit for review": PUTs the
  // current on-screen state and returns the updated detail, or null (having already
  // set actionError) on failure. No toast here - callers decide what to show.
  async function saveDraftAsync(): Promise<MovieDraftDetail | null> {
    const response = await bffFetch(`/bff/api/drafts/movies/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        title,
        genre,
        description,
        yearOfRelease,
        formatVariants: variants.map((variant) => ({
          id: variant.id,
          format: variant.format,
          price: Number(variant.price),
        })),
      }),
    });

    if (!response.ok) {
      setActionError("Failed to save this draft.");
      return null;
    }

    const data = (await response.json()) as MovieDraftDetail;
    setDetail(data);
    setVariants(toEditableVariants(data));
    return data;
  }

  async function handleSave(): Promise<void> {
    setActionError(null);
    setSaving(true);
    try {
      const data = await saveDraftAsync();
      if (data) {
        notify({ title: "Draft saved" });
      }
    } finally {
      setSaving(false);
    }
  }

  async function handleImageUpload(file: File): Promise<void> {
    setActionError(null);
    setUploadingImage(true);
    try {
      const formData = new FormData();
      formData.append("file", file);

      const response = await bffFetch(`/bff/api/drafts/movies/${id}/images`, {
        method: "POST",
        body: formData,
      });

      if (!response.ok) {
        setActionError("Failed to upload the cover image.");
        return;
      }

      // The upload endpoint returns only a DraftImageDto, so merge it into the draft
      // detail instead of replacing it.
      const image = (await response.json()) as DraftImageDto;
      setDetail((current) => (current ? { ...current, coverImage: image } : current));
    } finally {
      setUploadingImage(false);
    }
  }

  async function handleImageDelete(imageId: string): Promise<void> {
    setActionError(null);
    setUploadingImage(true);
    try {
      const response = await bffFetch(`/bff/api/drafts/movies/${id}/images/${imageId}`, {
        method: "DELETE",
      });

      if (!response.ok) {
        setActionError("Failed to remove the cover image.");
        return;
      }

      setDetail((current) => (current ? { ...current, coverImage: null } : current));
    } finally {
      setUploadingImage(false);
    }
  }

  // Auto-saves the current on-screen state before submitting, so "Submit for review"
  // always acts on what the Seller sees, not a stale, previously-saved snapshot.
  async function handleSubmitForReview(): Promise<void> {
    setActionError(null);
    setSubmitting(true);
    try {
      const saved = await saveDraftAsync();
      if (!saved) {
        return;
      }

      const response = await bffFetch(`/bff/api/drafts/movies/${id}/submit`, { method: "POST" });
      if (!response.ok) {
        const message = await response.text();
        setActionError(message || "Failed to submit this draft.");
        return;
      }

      // The submit endpoint returns a SubmissionSummary, so only update the local
      // status.
      setDetail((current) => (current ? { ...current, status: "PendingReview" } : current));
      notify({ title: "Draft saved and submitted for review" });
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCancelReview(): Promise<void> {
    setActionError(null);
    setCancelling(true);
    try {
      const response = await bffFetch(`/bff/api/drafts/movies/${id}/cancel-review`, { method: "POST" });
      if (!response.ok) {
        setActionError("Failed to cancel this review.");
        return;
      }

      // The cancel-review endpoint returns no response body, so only revert the local
      // status.
      setDetail((current) => (current ? { ...current, status: "Draft" } : current));
      notify({ title: "Review cancelled" });
    } finally {
      setCancelling(false);
    }
  }

  async function handleDelete(): Promise<void> {
    if (!window.confirm("Delete this draft? This can't be undone.")) {
      return;
    }

    setActionError(null);
    setDeleting(true);
    try {
      const response = await bffFetch(`/bff/api/drafts/movies/${id}`, { method: "DELETE" });
      if (!response.ok) {
        setActionError("Failed to delete this draft.");
        return;
      }

      router.push("/drafts");
    } finally {
      setDeleting(false);
    }
  }

  if (approvedAndRemoved) {
    return (
      <PageContainer maxWidth="2xl">
        <Alert>
          <AlertTitle>Approved</AlertTitle>
          <AlertDescription>
            This item was approved by the Merchandiser and is no longer editable. Check
            your{" "}
            <Link href="/submissions" className="underline">
              Submissions
            </Link>{" "}
            list for its assigned SKU.
          </AlertDescription>
        </Alert>
      </PageContainer>
    );
  }

  if (loadError) {
    return (
      <PageContainer maxWidth="2xl">
        <p role="alert" className="text-sm text-destructive">
          {loadError}
        </p>
      </PageContainer>
    );
  }

  if (!detail) {
    return (
      <PageContainer maxWidth="2xl">
        <p className="text-sm text-muted-foreground">Loading draft…</p>
      </PageContainer>
    );
  }

  const canSubmitForReview = detail.coverImage !== null && variants.length >= 1;
  const isDraftStatus = detail.status === "Draft";
  const isPendingReviewStatus = detail.status === "PendingReview";

  return (
    <PageContainer maxWidth="2xl">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{detail.title || "Untitled movie"}</h1>
        <Badge variant={isDraftStatus ? "outline" : "secondary"}>{formatStatusLabel(detail.status)}</Badge>
      </div>

      {detail.lastRejectionReason && (
        <Alert variant="destructive">
          <AlertTitle>Rejected</AlertTitle>
          <AlertDescription>{detail.lastRejectionReason}</AlertDescription>
        </Alert>
      )}

      {actionError && (
        <p role="alert" className="text-sm text-destructive">
          {actionError}
        </p>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Details</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="title">Title</Label>
            <Input
              id="title"
              placeholder="e.g. The Great Adventure"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              disabled={!isDraftStatus}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="genre">Genre</Label>
            <Input
              id="genre"
              placeholder="e.g. Action, Comedy, Drama"
              value={genre}
              onChange={(e) => setGenre(e.target.value)}
              disabled={!isDraftStatus}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              placeholder="A short summary shoppers will see on the product page."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              disabled={!isDraftStatus}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="yearOfRelease">Year of release</Label>
            <Input
              id="yearOfRelease"
              type="number"
              value={yearOfRelease}
              onChange={(e) => setYearOfRelease(Number(e.target.value))}
              disabled={!isDraftStatus}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Format variants</CardTitle>
          <p className="text-sm text-muted-foreground">
            Each format (DVD, Blu-ray, streaming) is sold separately and needs its own price.
          </p>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {variants.length === 0 && <p className="text-sm text-muted-foreground">No format variants yet.</p>}
          {variants.length > 0 && (
            <div className="grid grid-cols-[1fr_1fr_auto] gap-2 px-0.5 text-xs font-medium text-muted-foreground">
              <span>Format</span>
              <span>Price</span>
              <span className="sr-only">Actions</span>
            </div>
          )}
          {variants.map((variant) => (
            <div key={variant.key} className="grid grid-cols-[1fr_1fr_auto] items-center gap-2">
              <Select
                value={variant.format}
                onValueChange={(value) => updateVariant(variant.key, { format: value as MovieFormat })}
                disabled={!isDraftStatus}
              >
                <SelectTrigger aria-label="Format" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MOVIE_FORMATS.map((format) => (
                    <SelectItem key={format} value={format}>
                      {format}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Input
                aria-label="Price"
                type="number"
                inputMode="decimal"
                min={0}
                step={0.01}
                placeholder="0.00"
                value={variant.price}
                onChange={(e) => updateVariant(variant.key, { price: e.target.value })}
                disabled={!isDraftStatus}
              />
              <Button
                type="button"
                variant="outline"
                onClick={() => removeVariant(variant.key)}
                disabled={!isDraftStatus}
              >
                Remove
              </Button>
            </div>
          ))}
          <Button type="button" variant="secondary" onClick={addVariant} disabled={!isDraftStatus}>
            Add format variant
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Cover image</CardTitle>
          <p className="text-sm text-muted-foreground">
            This is the picture shoppers see on the product page. One image, up to 5 MB
            (JPEG, PNG, or WebP).
          </p>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {detail.coverImage && (
            <div className="flex flex-col gap-2">
              {/* eslint-disable-next-line @next/next/no-img-element -- streamed through the BFF proxy, not a static/optimizable asset. */}
              <img
                src={`/bff/api/drafts/movies/${id}/images/${detail.coverImage.id}`}
                alt={`Cover art for ${title || "this movie"}`}
                className="h-48 w-32 rounded-lg border object-cover"
              />
              <Button
                type="button"
                variant="outline"
                onClick={() => void handleImageDelete(detail.coverImage!.id)}
                disabled={!isDraftStatus || uploadingImage}
              >
                Remove cover image
              </Button>
            </div>
          )}
          {!detail.coverImage && (
            <Input
              type="file"
              accept="image/jpeg,image/png,image/webp"
              disabled={!isDraftStatus || uploadingImage}
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) {
                  void handleImageUpload(file);
                }
              }}
            />
          )}
        </CardContent>
      </Card>

      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-3">
          <Button
            type="button"
            onClick={() => void handleSave()}
            disabled={!isDraftStatus || saving || submitting}
          >
            Save changes
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={() => void handleSubmitForReview()}
            disabled={!isDraftStatus || !canSubmitForReview || saving || submitting}
          >
            Submit for review
          </Button>
          {isPendingReviewStatus && (
            <Button
              type="button"
              variant="outline"
              onClick={() => void handleCancelReview()}
              disabled={cancelling}
            >
              Cancel review
            </Button>
          )}
          <Button
            type="button"
            variant="destructive"
            className="ml-auto"
            onClick={() => void handleDelete()}
            disabled={!isDraftStatus || deleting}
          >
            Delete draft
          </Button>
        </div>
        {isDraftStatus && !canSubmitForReview && (
          <p className="text-sm text-muted-foreground">
            Add exactly one cover image and at least one format variant before submitting.
          </p>
        )}
        {isPendingReviewStatus && (
          <p className="text-sm text-muted-foreground">
            This draft is under review and can&apos;t be edited right now. Cancel the
            review to resume editing and resubmit it.
          </p>
        )}
        {!isDraftStatus && !isPendingReviewStatus && (
          <p className="text-sm text-muted-foreground">
            This draft can&apos;t be edited right now.
          </p>
        )}
      </div>
    </PageContainer>
  );
}
