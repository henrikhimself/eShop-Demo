"use client";

import { Toast } from "@base-ui/react/toast";
import type { ReactNode } from "react";
import { PortalNav } from "@/components/portal-nav";
import { SubmissionEvents } from "@/components/submission-events";
import { ToastViewport } from "@/components/toast-viewport";
import { toastManager } from "@/lib/toast";

// A route group, not a URL segment - /drafts and /submissions keep their existing paths.
export default function PortalLayout({ children }: { children: ReactNode }) {
  return (
    <Toast.Provider timeout={3000} toastManager={toastManager}>
      <div className="min-h-screen bg-muted/30">
        <PortalNav />
        <SubmissionEvents />
        {children}
      </div>
      <ToastViewport />
    </Toast.Provider>
  );
}
