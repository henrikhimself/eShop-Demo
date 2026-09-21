import { Toast } from "@base-ui/react/toast";

// Shared instance so notify() can be called from anywhere without React context;
// mounted via Toast.Provider's `toastManager` prop in app/(portal)/layout.tsx.
// Namespaced under `Toast.createToastManager`, not a top-level export (see
// index.parts.d.ts).
export const toastManager = Toast.createToastManager();

export interface NotifyOptions {
  title: string;
  description?: string;
}

// Thin wrapper so call sites don't repeat Base UI's raw add() option shape.
export function notify({ title, description }: NotifyOptions): void {
  toastManager.add({ title, description, timeout: 3000 });
}
