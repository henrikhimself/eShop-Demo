"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";

const NAV_LINKS = [
  { href: "/drafts", label: "Drafts" },
  { href: "/submissions", label: "Submissions" },
  { href: "/inventory", label: "Inventory" },
] as const;

export function PortalNav() {
  const pathname = usePathname();

  return (
    <header className="border-b bg-background">
      <div className="mx-auto flex w-full max-w-4xl items-center justify-between px-6 py-3">
        <Link href="/drafts" className="font-semibold">
          Seller Portal
        </Link>
        <nav className="flex items-center gap-4 text-sm">
          {NAV_LINKS.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className={cn(
                "transition-colors hover:text-foreground",
                pathname.startsWith(link.href) ? "font-medium text-foreground" : "text-muted-foreground",
              )}
            >
              {link.label}
            </Link>
          ))}
          {/*
            Not next/link: /bff/logout is a Route Handler that ends in a redirect, not a
            Next.js page - same reasoning as the /bff/login link on the landing page.
          */}
          {/* eslint-disable-next-line @next/next/no-html-link-for-pages */}
          <a href="/bff/logout" className="text-muted-foreground transition-colors hover:text-foreground">
            Log out
          </a>
        </nav>
      </div>
    </header>
  );
}
