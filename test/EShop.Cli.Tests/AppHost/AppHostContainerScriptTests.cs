// <copyright file="AppHostContainerScriptTests.cs" company="Henrik Jensen">
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

using Hj.EShop.Cli.AppHost;
using Xunit;

namespace Hj.EShop.Cli.Tests.AppHost;

public sealed class AppHostContainerScriptTests
{
    [Fact]
    public void Build_IncludesStartStopAndKeycloakPoll()
    {
        string script = AppHostContainerScript.Build("src/EShop.AppHost/EShop.AppHost.csproj", []);

        Assert.Contains("aspire start --no-build --non-interactive --apphost 'src/EShop.AppHost/EShop.AppHost.csproj'", script, StringComparison.Ordinal);
        Assert.Contains("aspire stop --non-interactive --apphost 'src/EShop.AppHost/EShop.AppHost.csproj'", script, StringComparison.Ordinal);
        Assert.Contains("keycloak-", script, StringComparison.Ordinal);
        Assert.Contains("trap 'hj_stop; exit 0' INT TERM", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ExtraArguments_AreShellQuotedAndAppended()
    {
        string script = AppHostContainerScript.Build("apphost.csproj", ["--launch-profile", "it's a test"]);

        Assert.Contains("'--launch-profile' 'it'\\''s a test'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_StdinEofDoesNotStopTheAppHost()
    {
        string script = AppHostContainerScript.Build("apphost.csproj", []);

        Assert.Contains("X_QUIT=1", script, StringComparison.Ordinal);
        Assert.Contains("tail -f /dev/null &", script, StringComparison.Ordinal);
        Assert.Contains("wait $!", script, StringComparison.Ordinal);
    }
}
