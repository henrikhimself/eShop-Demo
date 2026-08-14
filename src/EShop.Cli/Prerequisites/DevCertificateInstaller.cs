// <copyright file="DevCertificateInstaller.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Repo;

namespace Hj.EShop.Cli.Prerequisites;

internal sealed class DevCertificateInstaller(
    ILocalToolLocator localToolLocator,
    IProcessRunner processRunner,
    IContainerRunner containerRunner,
    RepoPaths paths) : IDevCertificateInstaller
{
    private const string DevCertPassword = "dev-password";

    public async Task EnsureTrustedAsync(CancellationToken cancellationToken)
    {
        if (!localToolLocator.IsOnPath("dotnet"))
        {
            // No local .NET SDK to export the cert from - best-effort container trust only.
            await containerRunner.RunAsync(new ToolInvocation("dotnet", ["dev-certs", "https", "--trust"]), cancellationToken);
            return;
        }

        string devCertPath = Path.Combine(paths.TmpDir, "devcert.pfx");
        if (File.Exists(devCertPath))
        {
            return;
        }

        // Exporting must always happen locally, never via IToolExecutor's --tools
        // routing - it has to reach *this* machine's dev-certs store regardless of
        // --tools container.
        await processRunner.RunAsync(
            new ProcessRequest("dotnet", ["dev-certs", "https", "--export-path", devCertPath, "--password", DevCertPassword]),
            cancellationToken);

        await ImportIntoContainerNssDatabaseAsync(devCertPath, cancellationToken);
    }

    private async Task ImportIntoContainerNssDatabaseAsync(string devCertPath, CancellationToken cancellationToken)
    {
        string relativeDevCertPath = Path.GetRelativePath(paths.Root, devCertPath);

        // Imports the exported cert into the utility container's NSS database (used by
        // Chromium/Playwright for `eshop test e2e`) and marks it done so
        // `GlobalOptionsInterceptor` running this before every `eshop` command stays a
        // no-op after the first successful import.
        string script =
            $"""
            nssdb="$HOME/.pki/nssdb"
            marker="$nssdb/.import-devcert.done"
            if [ -f "$marker" ]; then exit 0; fi
            mkdir -p "$nssdb"
            certutil -N -d "sql:$nssdb" --empty-password \
              && pk12util -d "sql:$nssdb" -i "{relativeDevCertPath}" -W "{DevCertPassword}" \
              && dotnet dev-certs https --clean --import "{relativeDevCertPath}" --password "{DevCertPassword}" \
              && dotnet dev-certs https --trust \
              && touch "$marker"
            """;

        await containerRunner.RunAsync(new ToolInvocation("sh", ["-c", script]), cancellationToken);
    }
}
