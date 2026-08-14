"use client";

import {
  Toast,
  ToastAction,
  ToastClose,
  ToastContent,
  ToastDescription,
  ToastPortal,
  ToastTitle,
  ToastViewport as ToastViewportPrimitive,
  useToastManager,
} from "@/components/ui/toast";

// Renders the active toast queue via context; mounted once in Toast.Provider
// (app/(portal)/layout.tsx).
export function ToastViewport() {
  const { toasts } = useToastManager();

  return (
    <ToastPortal>
      <ToastViewportPrimitive>
        {toasts.map((toast) => (
          <Toast key={toast.id} toast={toast}>
            <ToastContent>
              <div className="flex min-w-0 flex-1 flex-col gap-1">
                <ToastTitle />
                <ToastDescription />
              </div>
              <ToastAction />
              <ToastClose />
            </ToastContent>
          </Toast>
        ))}
      </ToastViewportPrimitive>
    </ToastPortal>
  );
}
