import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export default function Home() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/30 p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-2xl">Seller Portal</CardTitle>
          <CardDescription>Manage your movie and merchandise listings.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {/* Not next/link: /bff/login redirects to Keycloak; next/link's client-side
              RSC navigation can't follow that and throws, so a full navigation is needed. */}
          {/* eslint-disable-next-line @next/next/no-html-link-for-pages */}
          <a href="/bff/login" className={buttonVariants({ className: "w-full" })}>
            Log in
          </a>
          <Link href="/drafts" className="text-center text-sm text-muted-foreground hover:text-foreground">
            Already logged in? Go to your drafts
          </Link>
        </CardContent>
      </Card>
    </main>
  );
}
