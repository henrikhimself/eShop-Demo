import { describe, expect, it, vi } from "vitest";
import { notify, toastManager } from "./toast";

describe("notify", () => {
  it("adds a toast with a 3s timeout and the given title/description", () => {
    const addSpy = vi.spyOn(toastManager, "add");

    notify({ title: "Draft saved", description: "extra detail" });

    expect(addSpy).toHaveBeenCalledWith({
      title: "Draft saved",
      description: "extra detail",
      timeout: 3000,
    });

    addSpy.mockRestore();
  });

  it("omits description when none is given", () => {
    const addSpy = vi.spyOn(toastManager, "add");

    notify({ title: "Submitted for review" });

    expect(addSpy).toHaveBeenCalledWith({
      title: "Submitted for review",
      description: undefined,
      timeout: 3000,
    });

    addSpy.mockRestore();
  });
});
