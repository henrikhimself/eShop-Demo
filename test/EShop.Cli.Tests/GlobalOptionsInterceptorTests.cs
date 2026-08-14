// <copyright file="GlobalOptionsInterceptorTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.Commands;
using Hj.EShop.Cli.Execution;
using Hj.EShop.Cli.Output;
using Hj.EShop.Cli.Prerequisites;
using Spectre.Console.Cli;
using Xunit;

namespace Hj.EShop.Cli.Tests;

public sealed class GlobalOptionsInterceptorTests
{
    [Fact]
    public void Intercept_GlobalSettings_ResolvesOptionsAndRunsBothChecksExactlyOnce()
    {
        GlobalOptionsAccessor accessor = new();
        FakeDevCertificateInstaller devCertificateInstaller = new();
        FakePrerequisiteChecker prerequisiteChecker = new([]);
        GlobalOptionsInterceptor interceptor = new(accessor, devCertificateInstaller, prerequisiteChecker);
        DefaultSettings settings = new() { Tools = ExecutionMode.Local, Agent = true };

        interceptor.Intercept(context: null!, settings);

        Assert.Equal(ExecutionMode.Local, accessor.Options.Tools);
        Assert.Equal(OutputMode.Ai, accessor.Options.Output);
        Assert.Equal(1, devCertificateInstaller.CallCount);
        Assert.Equal(1, prerequisiteChecker.CallCount);
    }

    [Fact]
    public void Intercept_NonGlobalSettings_ResolvesNothingAndRunsNeitherCheck()
    {
        GlobalOptionsAccessor accessor = new();
        FakeDevCertificateInstaller devCertificateInstaller = new();
        FakePrerequisiteChecker prerequisiteChecker = new([]);
        GlobalOptionsInterceptor interceptor = new(accessor, devCertificateInstaller, prerequisiteChecker);

        interceptor.Intercept(context: null!, new NonGlobalSettings());

        Assert.Equal(0, devCertificateInstaller.CallCount);
        Assert.Equal(0, prerequisiteChecker.CallCount);
    }

    [Fact]
    public void Intercept_PrerequisiteIssuesFound_DoesNotThrow()
    {
        GlobalOptionsAccessor accessor = new();
        FakeDevCertificateInstaller devCertificateInstaller = new();
        FakePrerequisiteChecker prerequisiteChecker = new(["dotnet: version mismatch"]);
        GlobalOptionsInterceptor interceptor = new(accessor, devCertificateInstaller, prerequisiteChecker);

        Exception? exception = Record.Exception(() => interceptor.Intercept(context: null!, new DefaultSettings()));

        Assert.Null(exception);
        Assert.Equal(1, prerequisiteChecker.CallCount);
    }

    [Fact]
    public void IssuesToShow_LocalToolIssuesOnly_HiddenUnderToolsAuto()
    {
        PrerequisiteCheckResult result = new([], ["node: version mismatch"], new HashSet<string> { "node" });

        IReadOnlyList<string> issuesToShow = GlobalOptionsInterceptor.IssuesToShow(result, ExecutionMode.Auto);

        Assert.Empty(issuesToShow);
    }

    [Fact]
    public void IssuesToShow_LocalToolIssuesOnly_HiddenUnderToolsContainer()
    {
        PrerequisiteCheckResult result = new([], ["node: version mismatch"], new HashSet<string> { "node" });

        IReadOnlyList<string> issuesToShow = GlobalOptionsInterceptor.IssuesToShow(result, ExecutionMode.Container);

        Assert.Empty(issuesToShow);
    }

    [Fact]
    public void IssuesToShow_LocalToolIssues_ShownWhenToolsModeIsLocal()
    {
        PrerequisiteCheckResult result = new([], ["node: version mismatch"], new HashSet<string> { "node" });

        IReadOnlyList<string> issuesToShow = GlobalOptionsInterceptor.IssuesToShow(result, ExecutionMode.Local);

        Assert.Equal(["node: version mismatch"], issuesToShow);
    }

    [Fact]
    public void IssuesToShow_GeneralIssues_AlwaysShownUnderToolsAuto()
    {
        PrerequisiteCheckResult result = new(["host architecture: Arm64"], [], new HashSet<string>());

        IReadOnlyList<string> issuesToShow = GlobalOptionsInterceptor.IssuesToShow(result, ExecutionMode.Auto);

        Assert.Equal(["host architecture: Arm64"], issuesToShow);
    }

    [Fact]
    public void IssuesToShow_GeneralIssues_AlwaysShownUnderToolsContainer()
    {
        PrerequisiteCheckResult result = new(["host architecture: Arm64"], [], new HashSet<string>());

        IReadOnlyList<string> issuesToShow = GlobalOptionsInterceptor.IssuesToShow(result, ExecutionMode.Container);

        Assert.Equal(["host architecture: Arm64"], issuesToShow);
    }

    [Fact]
    public void IssuesToShow_GeneralIssues_AlwaysShownUnderToolsLocal()
    {
        PrerequisiteCheckResult result = new(["host architecture: Arm64"], [], new HashSet<string>());

        IReadOnlyList<string> issuesToShow = GlobalOptionsInterceptor.IssuesToShow(result, ExecutionMode.Local);

        Assert.Equal(["host architecture: Arm64"], issuesToShow);
    }

    [Fact]
    public void IssuesToShow_NothingToReport_ReturnsEmpty()
    {
        PrerequisiteCheckResult result = new([], [], new HashSet<string>());

        IReadOnlyList<string> issuesToShow = GlobalOptionsInterceptor.IssuesToShow(result, ExecutionMode.Local);

        Assert.Empty(issuesToShow);
    }

    private sealed class NonGlobalSettings : CommandSettings
    {
    }

    private sealed class FakeDevCertificateInstaller : IDevCertificateInstaller
    {
        public int CallCount { get; private set; }

        public Task EnsureTrustedAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePrerequisiteChecker(IReadOnlyList<string> generalIssues) : IPrerequisiteChecker
    {
        public int CallCount { get; private set; }

        public Task<PrerequisiteCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new PrerequisiteCheckResult(generalIssues, [], new HashSet<string>()));
        }
    }
}
