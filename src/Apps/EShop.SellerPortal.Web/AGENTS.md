<!-- BEGIN:nextjs-agent-rules -->
# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` before writing any code. Heed deprecation notices.
<!-- END:nextjs-agent-rules -->

## Conventions

- `PageContainer` (`components/page-container.tsx`) is used by every page under
  `app/(portal)/`: draft editor pages pass `maxWidth="2xl"`; list/table pages use the
  wider default (`4xl`).
