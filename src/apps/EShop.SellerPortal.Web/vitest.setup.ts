import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

// Not automatic here: RTL's built-in auto-cleanup relies on detecting a global
// `afterEach`, which this project doesn't enable (vitest.config.ts has no
// `test.globals`). Without this, DOM from one test in a file leaks into the next.
afterEach(() => {
  cleanup();
});
