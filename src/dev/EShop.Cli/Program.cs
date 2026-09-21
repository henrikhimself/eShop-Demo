// <copyright file="Program.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli;
using Hj.EShop.Cli.AppHost;
using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Commands.Diagram;
using Hj.EShop.Cli.Commands.Generate;
using Hj.EShop.Cli.Commands.Test;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Prerequisites;
using Hj.EShop.Cli.Repo;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

// The CLI is always launched from (or exec'd against) a repo checkout - see
// scripts/eshop.sh - so the repo root is resolved once, here, rather than by
// every command separately.
GlobalOptionsAccessor globalOptionsAccessor = new();
RepoRootLocator repoRootLocator = new();
RepoPaths repoPaths = new(repoRootLocator.Find());

TypeRegistrar registrar = new(new ServiceCollection());
registrar.RegisterInstance(typeof(IGlobalOptionsAccessor), globalOptionsAccessor);
registrar.RegisterInstance(typeof(GlobalOptionsAccessor), globalOptionsAccessor);
registrar.RegisterInstance(typeof(RepoPaths), repoPaths);
registrar.Register(typeof(IRepoRootLocator), typeof(RepoRootLocator));
registrar.Register(typeof(IPinnedVersionReader), typeof(PinnedVersionReader));
registrar.Register(typeof(IProcessRunner), typeof(ProcessRunner));
registrar.Register(typeof(ICommandInterceptor), typeof(GlobalOptionsInterceptor));
registrar.Register(typeof(ILocalToolLocator), typeof(LocalToolLocator));
registrar.Register(typeof(ILinuxIdentityProvider), typeof(LinuxIdentityProvider));
registrar.Register(typeof(IUtilityImageSourceHasher), typeof(UtilityImageSourceHasher));
registrar.Register(typeof(IUtilityImageProvisioner), typeof(UtilityImageProvisioner));
registrar.Register(typeof(IContainerRunner), typeof(ContainerRunner));
registrar.Register(typeof(IToolExecutor), typeof(ToolExecutor));
registrar.Register(typeof(IPrerequisiteChecker), typeof(PrerequisiteChecker));
registrar.Register(typeof(IDevCertificateInstaller), typeof(DevCertificateInstaller));
registrar.Register(typeof(IApiSchemaGenerator), typeof(ApiSchemaGenerator));
registrar.Register(typeof(IAppHostGuard), typeof(AppHostGuard));
registrar.Register(typeof(IConsoleKeyReader), typeof(ConsoleKeyReader));
registrar.Register(typeof(IAppHostSessionRunner), typeof(AppHostSessionRunner));

// Deferred and memoized: IOutputSink can only resolve correctly after
// GlobalOptionsInterceptor has run, and the CLI's final flush below needs to reach
// the exact same instance a command used.
IOutputSink? outputSink = null;
Func<IOutputSink> outputSinkFactory = () => outputSink ??= OutputSinkFactory.Create(
    globalOptionsAccessor.Options.Output, globalOptionsAccessor.Options.Debug);
registrar.RegisterLazy(typeof(IOutputSink), () => outputSinkFactory());

// UtilityImageProvisioner needs the same factory - see its own comment.
registrar.RegisterInstance(typeof(Func<IOutputSink>), outputSinkFactory);

CommandApp app = new(registrar);
app.Configure(config =>
{
    config.SetApplicationName("eshop");
    config.SetApplicationVersion(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown");

    config.AddCommand<RestoreCommand>("restore")
        .WithDescription("Restore NuGet and frontend dependencies.");
    config.AddCommand<BuildCommand>("build")
        .WithDescription("Build the solution and report every diagnostic in one pass.");
    config.AddCommand<FormatCommand>("format")
        .WithDescription("Apply every auto-fixable formatting/lint fix.");
    config.AddCommand<CleanCommand>("clean")
        .WithDescription("Delete bin/obj, Next.js build caches, and tmp/ (never node_modules, .cache/, or the utility image).");

    config.AddBranch<TestSettings>("test", test =>
    {
        test.SetDescription("Run the .NET and frontend test suites (default: unit tests only).");
        test.SetDefaultCommand<UnitTestCommand>();
        test.AddCommand<E2ETestCommand>("e2e")
            .WithDescription("Run the browser end-to-end test suite (always containerized).");
        test.AddCommand<CoverageTestCommand>("coverage")
            .WithDescription("Run tests under coverage instrumentation.");
    });

    config.AddCommand<RunCommand>("run")
        .WithDescription("Start the Aspire AppHost (dashboard, live logs, hot reload).");

    config.AddCommand<ScreenshotCommand>("screenshot")
        .WithDescription("Screenshot a URL using headless Chromium (always containerized).");

    config.AddBranch<DefaultSettings>("diagram", diagram =>
        diagram.AddCommand<RenderDiagramCommand>("render")
            .WithDescription("Render a C4-PlantUML diagram to SVG."));

    config.AddBranch<DefaultSettings>("generate", generate =>
        generate.AddCommand<GenerateTypesCommand>("types")
            .WithDescription("Regenerate the Seller Portal Web's TypeScript API types."));
});

// Ctrl+C must reach RunCommand's AppHostSessionRunner so it can run `aspire stop`
// before exiting, rather than the process dying immediately and orphaning the AppHost.
using CancellationTokenSource cancellationTokenSource = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

int exitCode = await app.RunAsync(args, cancellationTokenSource.Token);

// Every command already calls this itself before printing its own results - this is
// just the final safety net, in case a command's last step is also its last action
// (nothing printed afterward to trigger its own flush) or one was missed.
if (outputSink is not null)
{
    await outputSink.FlushPendingStepsAsync();
}

return exitCode;
