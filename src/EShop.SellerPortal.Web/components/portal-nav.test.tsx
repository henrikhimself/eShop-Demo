import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { PortalNav } from "./portal-nav";

vi.mock("next/navigation", () => ({
  usePathname: () => "/drafts",
}));

describe("PortalNav", () => {
  it("links to Drafts, Submissions, and Inventory, and to /bff/logout", () => {
    render(<PortalNav />);

    expect(screen.getByRole("link", { name: "Drafts" })).toHaveAttribute("href", "/drafts");
    expect(screen.getByRole("link", { name: "Submissions" })).toHaveAttribute("href", "/submissions");
    expect(screen.getByRole("link", { name: "Inventory" })).toHaveAttribute("href", "/inventory");
    expect(screen.getByRole("link", { name: "Log out" })).toHaveAttribute("href", "/bff/logout");
  });
});
