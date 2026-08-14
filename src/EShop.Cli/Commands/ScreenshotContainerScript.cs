// <copyright file="ScreenshotContainerScript.cs" company="Henrik Jensen">
// Copyright 2026 Henrik Jensen
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace Hj.EShop.Cli.Commands;

// Generates the Node script `eshop screenshot` runs inside the utility container,
// using the Node Playwright driver installed by the Containerfile (globally, via
// npm - not a project package.json dependency). The script saves and loads
// Playwright's `storageState` (cookies + localStorage) under
// tmp/screenshot-profile so a login done once via --login survives across
// invocations. It uses `storageState`, not an on-disk `launchPersistentContext`
// Chrome profile, because the Seller Portal's auth cookie (see Program.cs's
// AddCookie call) is a session cookie with no client-visible expiry, which
// Chromium deletes from its on-disk cookie store on a clean exit regardless of
// profile persistence. `storageState` instead serializes cookies explicitly when
// called, independent of that exit-time behavior.
//
// Uses .cjs (not .mjs/.js): Node's ESM resolver does not honor NODE_PATH, only
// CommonJS's `require` does - and the Node `playwright` package is installed
// globally (see the Containerfile), resolved via NODE_PATH, not a local
// node_modules this repo's package.json would otherwise provide.
internal static class ScreenshotContainerScript
{
    public const string FileName = "screenshot-runner.cjs";

    public const string Script = """
        'use strict';

        const fs = require('fs');
        const path = require('path');
        const { chromium } = require('playwright');

        function parseArgs(argv) {
          const args = { login: false };
          for (let i = 0; i < argv.length; i += 1) {
            const arg = argv[i];
            if (arg === '--login') {
              args.login = true;
              continue;
            }

            if (arg.startsWith('--')) {
              args[arg.slice(2)] = argv[i + 1];
              i += 1;
            }
          }

          return args;
        }

        async function main() {
          const args = parseArgs(process.argv.slice(2));
          const width = Number.parseInt(args.width, 10);
          const height = Number.parseInt(args.height, 10);

          const profileDir = args['profile-dir'];
          fs.mkdirSync(profileDir, { recursive: true });
          const statePath = path.join(profileDir, 'state.json');

          const browser = await chromium.launch({ headless: true });
          try {
            const context = await browser.newContext({
              viewport: { width, height },
              ignoreHTTPSErrors: true,
              storageState: fs.existsSync(statePath) ? statePath : undefined,
            });

            try {
              const page = await context.newPage();

              if (args.login) {
                const origin = new URL(args.url).origin;
                await page.goto(`${origin}/bff/login`, { waitUntil: 'domcontentloaded' });
                try {
                  // A saved storage state with an already-valid session redirects
                  // straight back without ever showing the login form - only fill
                  // it in if it actually appears.
                  await page.waitForSelector('#username', { timeout: 5000 });
                  await page.fill('#username', args.username);
                  await page.fill('#password', args.password);
                  await page.click('#kc-login');
                } catch {
                  // Login form never appeared - already authenticated, nothing to do.
                }

                // Wait for the whole redirect chain (Keycloak -> BFF's
                // /bff/signin-oidc callback -> the hardcoded /drafts RedirectUri, see
                // AuthEndpoints.cs) to land back on a real app page at our origin
                // before the next navigation. Staying off /bff/* ensures the BFF has
                // finished setting the auth cookie and the requested URL remains the
                // final target.
                await page.waitForURL(
                  (url) => url.origin === origin && !url.pathname.startsWith('/bff/'),
                  { timeout: 15000 },
                );
              }

              await page.goto(args.url, { waitUntil: 'domcontentloaded' });

              // Network-idle wait with a hard wall-clock cap - whichever comes first.
              await Promise.race([
                page.waitForLoadState('networkidle'),
                new Promise((resolve) => {
                  setTimeout(resolve, 8000);
                }),
              ]);

              await page.screenshot({ path: args.output });

              // Always refresh the saved state, not just after --login: carries
              // forward a session that stayed valid (or renewed itself) so a later
              // invocation without --login can keep reusing it.
              await context.storageState({ path: statePath });
            } finally {
              await context.close();
            }
          } finally {
            await browser.close();
          }
        }

        main().catch((error) => {
          console.error(error);
          process.exitCode = 1;
        });
        """;
}
