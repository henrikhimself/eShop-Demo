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
import { Textarea } from "@/components/ui/textarea";
import { bffFetch } from "@/lib/bff-fetch";
import { formatStatusLabel } from "@/lib/format";
import { notify } from "@/lib/toast";
import type { DraftImageDto, MerchandiseDraftDetail } from "@/lib/types";

const MAX_IMAGES = 3;

export default function EditMerchandiseDraftPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();

  const [detail, setDetail] = useState<MerchandiseDraftDetail | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [approvedAndRemoved, setApprovedAndRemoved] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const [productName, setProductName] = useState("");
  const [description, setDescription] = useState("");
  const [price, setPrice] = useState("0");
  const [associatedMovieTitle, setAssociatedMovieTitle] = useState("");

  const [saving, setSaving] = useState(false);
  const [uploadingImage, setUploadingImage] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [deleting, setDeleting] = useState(false);

  // Shared by the initial load and the SSE-triggered live refresh below: fetches this
  // draft and applies it to state, or records why that failed.
  const refetchDraft = useCallback(async (): Promise<void> => {
    const response = await bffFetch(`/bff/api/drafts/merchandise/${id}`);
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

    const data = (await response.json()) as MerchandiseDraftDetail;
    setDetail(data);
    setProductName(data.productName);
    setDescription(data.description);
    setPrice(String(data.price));
    setAssociatedMovieTitle(data.associatedMovieTitle ?? "");
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

  // See the movie edit page's identical effect for the full rationale: refetching on
  // any event is safe because a Seller has at most one Pending submission per draft,
  // and form fields are already disabled while PendingReview or PendingImageCleanup.
  useEffect(() => {
    if (detail?.status !== "PendingReview" && detail?.status !== "PendingImageCleanup") {
      return;
    }

    const source = new EventSource("/bff/api/submissions/events");
    source.onmessage = () => void refetchDraft();

    return () => source.close();
  }, [detail?.status, refetchDraft]);

  // Shared by "Save changes" and the auto-save step of "Submit for review": PUTs the
  // current on-screen state and returns the updated detail, or null (having already
  // set actionError) on failure. No toast here - callers decide what to show.
  async function saveDraftAsync(): Promise<MerchandiseDraftDetail | null> {
    const response = await bffFetch(`/bff/api/drafts/merchandise/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        productName,
        description,
        price: Number(price),
        associatedMovieTitle: associatedMovieTitle || null,
      }),
    });

    if (!response.ok) {
      setActionError("Failed to save this draft.");
      return null;
    }

    const data = (await response.json()) as MerchandiseDraftDetail;
    setDetail(data);
    setProductName(data.productName);
    setDescription(data.description);
    setPrice(String(data.price));
    setAssociatedMovieTitle(data.associatedMovieTitle ?? "");
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

      const response = await bffFetch(`/bff/api/drafts/merchandise/${id}/images`, {
        method: "POST",
        body: formData,
      });

      if (!response.ok) {
        setActionError("Failed to upload the image.");
        return;
      }

      // The upload endpoint returns only a DraftImageDto, so merge it into images
      // instead of replacing the draft detail.
      const image = (await response.json()) as DraftImageDto;
      setDetail((current) => (current ? { ...current, images: [...current.images, image] } : current));
    } finally {
      setUploadingImage(false);
    }
  }

  async function handleImageDelete(imageId: string): Promise<void> {
    setActionError(null);
    setUploadingImage(true);
    try {
      const response = await bffFetch(`/bff/api/drafts/merchandise/${id}/images/${imageId}`, {
        method: "DELETE",
      });

      if (!response.ok) {
        setActionError("Failed to remove this image.");
        return;
      }

      setDetail((current) =>
        current ? { ...current, images: current.images.filter((image) => image.id !== imageId) } : current,
      );
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

      const response = await bffFetch(`/bff/api/drafts/merchandise/${id}/submit`, { method: "POST" });
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
      const response = await bffFetch(`/bff/api/drafts/merchandise/${id}/cancel-review`, { method: "POST" });
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
      const response = await bffFetch(`/bff/api/drafts/merchandise/${id}`, { method: "DELETE" });
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

  // Mirrors DraftValidation.ValidateMerchandiseDraft's one hard requirement (a
  // non-empty product name) - no new price/description minimum is invented here.
  const canSubmitForReview = productName.trim().length > 0;
  const isDraftStatus = detail.status === "Draft";
  const isPendingReviewStatus = detail.status === "PendingReview";
  const canAddImage = detail.images.length < MAX_IMAGES;

  return (
    <PageContainer maxWidth="2xl">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">{detail.productName || "Untitled merchandise"}</h1>
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
            <Label htmlFor="productName">Product name</Label>
            <Input
              id="productName"
              placeholder="e.g. Alien Xenomorph Figure"
              value={productName}
              onChange={(e) => setProductName(e.target.value)}
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
            <Label htmlFor="price">Price</Label>
            <Input
              id="price"
              type="number"
              inputMode="decimal"
              min={0}
              step={0.01}
              placeholder="0.00"
              value={price}
              onChange={(e) => setPrice(e.target.value)}
              disabled={!isDraftStatus}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="associatedMovieTitle">Associated movie</Label>
            <Input
              id="associatedMovieTitle"
              placeholder="e.g. Alien"
              value={associatedMovieTitle}
              onChange={(e) => setAssociatedMovieTitle(e.target.value)}
              disabled={!isDraftStatus}
            />
            <p className="text-sm text-muted-foreground">
              Optional - if this item is tied to a specific movie, enter its title.
            </p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Images</CardTitle>
          <p className="text-sm text-muted-foreground">
            Up to 3 images, JPEG, PNG, or WebP, 5 MB each. No images are required.
          </p>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {detail.images.length > 0 && (
            <div className="flex flex-wrap gap-3">
              {detail.images.map((image) => (
                <div key={image.id} className="flex flex-col gap-2">
                  {/* eslint-disable-next-line @next/next/no-img-element -- streamed through the BFF proxy, not a static/optimizable asset. */}
                  <img
                    src={`/bff/api/drafts/merchandise/${id}/images/${image.id}`}
                    alt={`${productName || "This item"}`}
                    className="h-32 w-32 rounded-lg border object-cover"
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => void handleImageDelete(image.id)}
                    disabled={!isDraftStatus || uploadingImage}
                  >
                    Remove
                  </Button>
                </div>
              ))}
            </div>
          )}
          {canAddImage && (
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
          {!canAddImage && <p className="text-sm text-muted-foreground">Up to 3 images.</p>}
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
          <p className="text-sm text-muted-foreground">Add a product name before submitting.</p>
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
