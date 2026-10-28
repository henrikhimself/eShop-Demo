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
    private const int MaxDiagnosticCharacters = 64 * 1024;

    public async Task EnsureTrustedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!localToolLocator.IsOnPath("dotnet"))
        {
            // No local SDK to export from. Trust the container's own certificate.
            ProcessResult trust = await containerRunner.RunAsync(
                new ToolInvocation("dotnet", ["dev-certs", "https", "--trust"],
                    MaxCapturedOutputCharacters: MaxDiagnosticCharacters),
                cancellationToken);
            EnsureSucceeded(trust, "trust the container development certificate");
            return;
        }

        string devCertPath = Path.Combine(paths.TmpDir, "devcert.pfx");
        string markerPath = Path.Combine(paths.CacheHomeDir, ".pki", "nssdb", ".import-devcert.done");
        if (File.Exists(devCertPath) && File.Exists(markerPath))
        {
            return;
        }

        if (!File.Exists(devCertPath))
        {
            // A new export invalidates any import marker left by an older certificate.
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }

            await ExportAsync(devCertPath, cancellationToken);
        }

        // An existing export is not proof of a successful import. Retry after a
        // failed import or after the container's shared home/cache was removed.
        await ImportIntoContainerNssDatabaseAsync(devCertPath, cancellationToken);
    }

    private async Task ExportAsync(string devCertPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.TmpDir);
        string temporaryPath = Path.Combine(paths.TmpDir, $"devcert-{Guid.NewGuid():N}.pfx");

        try
        {
            // Always local: this must reach this machine's dev-certs store, not
            // IToolExecutor's --tools routing. Publish the export only on success.
            ProcessResult export = await processRunner.RunAsync(
                new ProcessRequest("dotnet",
                    ["dev-certs", "https", "--export-path", temporaryPath, "--password", DevCertPassword],
                    MaxCapturedOutputCharacters: MaxDiagnosticCharacters),
                cancellationToken);
            EnsureSucceeded(export, "export the local development certificate");
            File.Move(temporaryPath, devCertPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private async Task ImportIntoContainerNssDatabaseAsync(string devCertPath, CancellationToken cancellationToken)
    {
        string relativeDevCertPath = Path.GetRelativePath(paths.Root, devCertPath);
        string quotedDevCertPath = "'" + relativeDevCertPath.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

        // The NSS database may already exist without our completion marker.
        // certutil -N on an initialized database changes its password; --empty-password
        // alone does not prevent that path from prompting for the old password.
        string script =
            $"""
            set -eu
            exec </dev/null
            nssdb="$HOME/.pki/nssdb"
            marker="$nssdb/.import-devcert.done"
            if [ -f "$marker" ]; then exit 0; fi
            mkdir -p "$nssdb"
            password_file="$(mktemp)"
            trap 'rm -f "$password_file"' 0
            printf '\n' > "$password_file"
            if [ ! -f "$nssdb/cert9.db" ] && [ ! -f "$nssdb/key4.db" ]; then
              certutil -N -d "sql:$nssdb" --empty-password -f "$password_file" -@ "$password_file"
            fi
            pk12util -d "sql:$nssdb" -i {quotedDevCertPath} -k "$password_file" -W "{DevCertPassword}"
            dotnet dev-certs https --clean --import {quotedDevCertPath} --password "{DevCertPassword}"
            dotnet dev-certs https --trust
            touch "$marker"
            """;

        ProcessResult import = await containerRunner.RunAsync(
            new ToolInvocation("sh", ["-c", script], MaxCapturedOutputCharacters: MaxDiagnosticCharacters),
            cancellationToken);
        EnsureSucceeded(import, "import and trust the development certificate in the utility container");
    }

    private static void EnsureSucceeded(ProcessResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to {action} (exit code {result.ExitCode}).\nLast captured output:\n{result.StandardOutput}{result.StandardError}");
        }
    }
}
