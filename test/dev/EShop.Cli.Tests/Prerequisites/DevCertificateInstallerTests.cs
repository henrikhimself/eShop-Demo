// <copyright file="DevCertificateInstallerTests.cs" company="Henrik Jensen">
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
using Hj.EShop.Cli.Prerequisites;
using Hj.EShop.Cli.Repo;
using Hj.EShop.Cli.Tests.Fakes;
using Xunit;

namespace Hj.EShop.Cli.Tests.Prerequisites;

public sealed class DevCertificateInstallerTests : IDisposable
{
    private readonly string _repoRoot = Directory.CreateTempSubdirectory("eshop-cli-tests-").FullName;

    [Fact]
    public async Task EnsureTrustedAsync_NoLocalDotnet_FallsBackToContainerTrustOnly()
    {
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = CreateInstaller(processRunner, containerRunner, localDotnet: false);

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        Assert.Empty(processRunner.Invocations);
        ToolInvocation invocation = Assert.Single(containerRunner.Invocations);
        Assert.Equal("dotnet", invocation.Tool);
        Assert.Equal(["dev-certs", "https", "--trust"], invocation.Arguments);
        Assert.Equal(64 * 1024, invocation.MaxCapturedOutputCharacters);
        Assert.Equal(TestContext.Current.CancellationToken, Assert.Single(containerRunner.CancellationTokens));
    }

    [Fact]
    public async Task EnsureTrustedAsync_ContainerTrustFails_ReportsFailure()
    {
        FakeContainerRunner containerRunner = new(_ => new ProcessResult(1, "", "trust failed"));
        DevCertificateInstaller installer = CreateInstaller(new FakeProcessRunner(), containerRunner, localDotnet: false);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => installer.EnsureTrustedAsync(TestContext.Current.CancellationToken));

        Assert.Contains("trust failed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureTrustedAsync_LocalDotnetAndNoExport_ExportsThenImports()
    {
        FakeProcessRunner processRunner = new(ExportSuccessfully);
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = CreateInstaller(processRunner, containerRunner, localDotnet: true);

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        ProcessRequest export = Assert.Single(processRunner.Invocations);
        Assert.Equal("dotnet", export.FileName);
        Assert.Contains("--export-path", export.Arguments);
        Assert.Equal(64 * 1024, export.MaxCapturedOutputCharacters);
        Assert.Equal("exported", await File.ReadAllTextAsync(DevCertPath, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(DevCertPath)!));
        ToolInvocation import = Assert.Single(containerRunner.Invocations);
        Assert.Equal("sh", import.Tool);
        Assert.False(import.Interactive);
        Assert.Equal(64 * 1024, import.MaxCapturedOutputCharacters);
        Assert.Equal(TestContext.Current.CancellationToken, Assert.Single(processRunner.CancellationTokens));
        Assert.Equal(TestContext.Current.CancellationToken, Assert.Single(containerRunner.CancellationTokens));
    }

    [Fact]
    public async Task EnsureTrustedAsync_ExportExistsWithoutMarker_RetriesImport()
    {
        await CreateExportAsync();
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = CreateInstaller(processRunner, containerRunner, localDotnet: true);

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        Assert.Empty(processRunner.Invocations);
        Assert.Single(containerRunner.Invocations);
    }

    [Fact]
    public async Task EnsureTrustedAsync_ExportAndCompletionMarkerExist_DoesNothing()
    {
        await CreateExportAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
        await File.WriteAllTextAsync(MarkerPath, "", TestContext.Current.CancellationToken);
        FakeProcessRunner processRunner = new();
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = CreateInstaller(processRunner, containerRunner, localDotnet: true);

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        Assert.Empty(processRunner.Invocations);
        Assert.Empty(containerRunner.Invocations);
    }

    [Fact]
    public async Task EnsureTrustedAsync_MarkerExistsWithoutExport_InvalidatesMarkerAndReimports()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
        await File.WriteAllTextAsync(MarkerPath, "", TestContext.Current.CancellationToken);
        FakeProcessRunner processRunner = new(ExportSuccessfully);
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = CreateInstaller(processRunner, containerRunner, localDotnet: true);

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        Assert.False(File.Exists(MarkerPath));
        Assert.Single(processRunner.Invocations);
        Assert.Single(containerRunner.Invocations);
    }

    [Fact]
    public async Task EnsureTrustedAsync_ExportFails_DiscardsPartialExportAndDoesNotImport()
    {
        FakeProcessRunner processRunner = new(request =>
        {
            ProcessResult export = ExportSuccessfully(request);
            return export with { ExitCode = 1, StandardError = "export failed" };
        });
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = CreateInstaller(processRunner, containerRunner, localDotnet: true);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => installer.EnsureTrustedAsync(TestContext.Current.CancellationToken));

        Assert.Contains("export failed", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(DevCertPath)!));
        Assert.Empty(containerRunner.Invocations);
    }

    [Fact]
    public async Task EnsureTrustedAsync_ExportCancelled_DiscardsPartialExport()
    {
        using CancellationTokenSource cancellation = new();
        FakeProcessRunner processRunner = new(request =>
        {
            Assert.True(ExportSuccessfully(request).Succeeded);
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = CreateInstaller(processRunner, containerRunner, localDotnet: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => installer.EnsureTrustedAsync(cancellation.Token));

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(DevCertPath)!));
        Assert.Empty(containerRunner.Invocations);
    }

    [Fact]
    public async Task EnsureTrustedAsync_ImportFails_RetainsExportAndRetriesNextInvocation()
    {
        FakeProcessRunner processRunner = new(ExportSuccessfully);
        int attempts = 0;
        FakeContainerRunner containerRunner = new(_ =>
            ++attempts == 1 ? new ProcessResult(1, "", "import failed") : new ProcessResult(0, "", ""));
        DevCertificateInstaller installer = CreateInstaller(processRunner, containerRunner, localDotnet: true);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => installer.EnsureTrustedAsync(TestContext.Current.CancellationToken));
        Assert.Contains("import failed", error.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(DevCertPath));
        Assert.False(File.Exists(MarkerPath));

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        Assert.Single(processRunner.Invocations);
        Assert.Equal(2, containerRunner.Invocations.Count);
    }

    [Fact]
    public async Task EnsureTrustedAsync_ImportScript_InitializesOnlyMissingDatabaseAndSuppliesPasswords()
    {
        await CreateExportAsync();
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = CreateInstaller(new FakeProcessRunner(), containerRunner, localDotnet: true);

        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);

        string script = Assert.Single(containerRunner.Invocations).Arguments[1];
        Assert.Contains("set -eu", script, StringComparison.Ordinal);
        Assert.Contains("if [ ! -f \"$nssdb/cert9.db\" ] && [ ! -f \"$nssdb/key4.db\" ]; then", script, StringComparison.Ordinal);
        Assert.Contains("--empty-password -f \"$password_file\" -@ \"$password_file\"", script, StringComparison.Ordinal);
        Assert.Contains("-k \"$password_file\" -W \"dev-password\"", script, StringComparison.Ordinal);
        Assert.EndsWith("touch \"$marker\"", script.TrimEnd(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnsureTrustedAsync_ImportScript_ReusesDatabaseAndRetriesFailedTrust(bool databaseExists)
    {
        await CreateExportAsync();
        FakeContainerRunner containerRunner = new();
        DevCertificateInstaller installer = CreateInstaller(new FakeProcessRunner(), containerRunner, localDotnet: true);
        await installer.EnsureTrustedAsync(TestContext.Current.CancellationToken);
        string script = Assert.Single(containerRunner.Invocations).Arguments[1];

        string home = Path.Combine(_repoRoot, "home");
        string bin = Path.Combine(_repoRoot, "bin");
        string database = Path.Combine(home, ".pki", "nssdb");
        Directory.CreateDirectory(database);
        Directory.CreateDirectory(bin);
        if (databaseExists)
        {
            await File.WriteAllTextAsync(Path.Combine(database, "cert9.db"), "", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(database, "key4.db"), "", TestContext.Current.CancellationToken);
        }

        await WriteExecutableAsync(Path.Combine(bin, "certutil"),
            """
            #!/bin/sh
            set -eu
            db="$HOME/.pki/nssdb"
            if [ -f "$db/cert9.db" ]; then echo unexpected-reinitialization >&2; exit 1; fi
            touch "$db/cert9.db" "$db/key4.db"
            echo init >> "$HOME/steps"
            """);
        await WriteExecutableAsync(Path.Combine(bin, "pk12util"),
            """
            #!/bin/sh
            echo import >> "$HOME/steps"
            """);
        await WriteExecutableAsync(Path.Combine(bin, "dotnet"),
            """
            #!/bin/sh
            if [ "$3" = "--trust" ]; then
              echo trust >> "$HOME/steps"
              if [ -f "$HOME/fail-trust" ]; then echo trust-failed >&2; exit 1; fi
            fi
            """);

        Dictionary<string, string> environment = new()
        {
            ["HOME"] = home,
            ["PATH"] = bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
        };
        ProcessRunner runner = new();
        ProcessRequest request = new("sh", ["-c", script], _repoRoot, environment);
        string marker = Path.Combine(database, ".import-devcert.done");
        string failureFlag = Path.Combine(home, "fail-trust");
        await File.WriteAllTextAsync(failureFlag, "", TestContext.Current.CancellationToken);

        ProcessResult failed = await runner.RunAsync(request, TestContext.Current.CancellationToken);
        Assert.False(failed.Succeeded);
        Assert.False(File.Exists(marker));

        File.Delete(failureFlag);
        ProcessResult retried = await runner.RunAsync(request, TestContext.Current.CancellationToken);
        Assert.True(retried.Succeeded, retried.StandardError);
        Assert.True(File.Exists(marker));

        string[] steps = await File.ReadAllLinesAsync(Path.Combine(home, "steps"), TestContext.Current.CancellationToken);
        Assert.Equal(databaseExists ? 0 : 1, steps.Count(step => step == "init"));
        Assert.Equal(2, steps.Count(step => step == "import"));
        Assert.Equal(2, steps.Count(step => step == "trust"));

        ProcessResult repeated = await runner.RunAsync(request, TestContext.Current.CancellationToken);
        Assert.True(repeated.Succeeded);
        Assert.Equal(steps, await File.ReadAllLinesAsync(Path.Combine(home, "steps"), TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        Directory.Delete(_repoRoot, recursive: true);
    }

    private string DevCertPath => Path.Combine(_repoRoot, "tmp", "devcert.pfx");

    private string MarkerPath => Path.Combine(_repoRoot, ".cache", "home", ".pki", "nssdb", ".import-devcert.done");

    private static ProcessResult ExportSuccessfully(ProcessRequest request)
    {
        int exportIndex = request.Arguments.ToList().IndexOf("--export-path");
        File.WriteAllText(request.Arguments[exportIndex + 1], "exported");

        return new ProcessResult(0, "", "");
    }

    private DevCertificateInstaller CreateInstaller(
        FakeProcessRunner processRunner, FakeContainerRunner containerRunner, bool localDotnet)
    {
        return new DevCertificateInstaller(
            localDotnet ? new FakeLocalToolLocator("dotnet") : new FakeLocalToolLocator(),
            processRunner, containerRunner, new RepoPaths(_repoRoot));
    }

    private static async Task WriteExecutableAsync(string path, string script)
    {
        await File.WriteAllTextAsync(path, script, TestContext.Current.CancellationToken);
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        else
        {
            throw new PlatformNotSupportedException("The developer CLI requires Linux.");
        }
    }

    private async Task CreateExportAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DevCertPath)!);
        await File.WriteAllTextAsync(DevCertPath, "exported", TestContext.Current.CancellationToken);
    }
}
