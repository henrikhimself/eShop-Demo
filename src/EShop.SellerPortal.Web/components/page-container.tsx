import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

interface PageContainerProps {
  children: ReactNode;
  className?: string;
  maxWidth?: "2xl" | "4xl";
}

const MAX_WIDTH_CLASSES: Record<NonNullable<PageContainerProps["maxWidth"]>, string> = {
  "2xl": "max-w-2xl",
  "4xl": "max-w-4xl",
};

// Used by every Seller Portal page under app/(portal)/: draft editor pages pass
// maxWidth="2xl", list/table pages use the wider default.
export function PageContainer({ children, className, maxWidth = "4xl" }: PageContainerProps) {
  return (
    <main className={cn("mx-auto flex w-full flex-col gap-6 px-6 py-10", MAX_WIDTH_CLASSES[maxWidth], className)}>
      {children}
    </main>
  );
}
