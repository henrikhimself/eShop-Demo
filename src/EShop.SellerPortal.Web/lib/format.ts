// Converts a PascalCase status value (as returned by the BFF's Contracts/*.cs records,
// e.g. "PendingReview") into a human-readable label ("Pending review") for display.
export function formatStatusLabel(status: string): string {
  const spaced = status.replace(/([a-z])([A-Z])/g, "$1 $2");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}
