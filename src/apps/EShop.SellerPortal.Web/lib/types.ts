// Generated from the Bff's OpenAPI document via openapi-typescript (see
// lib/api-schema.d.ts; regenerate with `eshop generate types`) - do not hand-edit
// either file. See ADR 0014.
import type { components } from "./api-schema";

export type DraftStatus = components["schemas"]["DraftStatus"];
export type SubmissionStatus = components["schemas"]["SubmissionStatus"];
export type MovieFormat = components["schemas"]["MovieFormat"];
export type DraftKind = components["schemas"]["DraftKind"];

export type DraftSummary = components["schemas"]["DraftSummary"];
export type FormatVariantDto = components["schemas"]["FormatVariantDto"];
export type DraftImageDto = components["schemas"]["DraftImageDto"];
export type MovieDraftDetail = components["schemas"]["MovieDraftDetail"];
export type UpdateFormatVariantRequest = components["schemas"]["UpdateFormatVariantRequest"];
export type UpdateMovieDraftRequest = components["schemas"]["UpdateMovieDraftRequest"];
export type MerchandiseDraftDetail = components["schemas"]["MerchandiseDraftDetail"];
export type UpdateMerchandiseDraftRequest = components["schemas"]["UpdateMerchandiseDraftRequest"];
export type SubmissionSummary = components["schemas"]["SubmissionSummary"];
export type InventorySummary = components["schemas"]["InventorySummary"];
export type InventorySyncStatus = components["schemas"]["InventorySyncStatus"];
export type ReportInventoryRequest = components["schemas"]["ReportInventoryRequest"];

// MOVIE_FORMATS is a runtime value openapi-typescript can't generate; the
// exhaustiveness object below turns drift from the Bff's MovieFormat enum into a tsc
// error instead of a silent mismatch.
const _movieFormatsExhaustive: { [K in MovieFormat]: true } = { Dvd: true, BluRay: true, StreamingKey: true };
export const MOVIE_FORMATS: readonly MovieFormat[] = Object.keys(_movieFormatsExhaustive) as MovieFormat[];
