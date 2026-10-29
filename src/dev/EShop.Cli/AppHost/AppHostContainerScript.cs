// <copyright file="AppHostContainerScript.cs" company="Henrik Jensen">
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

namespace Hj.EShop.Cli.AppHost;

// Builds the one-liner for the container-fallback AppHost session (no local `aspire`
// CLI) - must run as one continuous `docker run`, since Docker kills the whole process
// tree the moment PID 1 exits.
internal static class AppHostContainerScript
{
    public static string Build(
        string relativeAppHostProject,
        IReadOnlyList<string> extraArguments,
        string relativeCliBinary,
        IReadOnlyList<string> buildGlobalArguments,
        IReadOnlyList<string> runGlobalArguments)
    {
        string quotedAppHost = ShellScriptBuilder.Quote(relativeAppHostProject);
        string quotedExtraArguments = string.Join(' ', extraArguments.Select(ShellScriptBuilder.Quote));
        string quotedCliBinary = ShellScriptBuilder.Quote(relativeCliBinary);
        string quotedBuildGlobalArguments = string.Join(' ', buildGlobalArguments.Select(ShellScriptBuilder.Quote));
        string quotedRunGlobalArguments = string.Join(' ', runGlobalArguments.Select(ShellScriptBuilder.Quote));

        ShellScriptBuilder script = new();
        script.Append("set -euo pipefail");
        script.Append("hj_stop() {");
        script.Append($"  aspire stop --non-interactive --apphost {quotedAppHost}");

        // Only inside the container: `aspire stop`'s own success message doesn't mean
        // the teardown of sibling containers (Keycloak, the tunnel proxy) has actually
        // finished - it continues asynchronously for up to ~20s. Exiting the moment
        // `aspire stop` returns would tear down this container - that still-in-flight
        // cleanup included - via Docker's own PID-1-exit lifecycle. Polls rather than
        // a fixed sleep, so a faster teardown doesn't always cost the full worst case.
        script.Append("  for _ in $(seq 1 30); do");
        script.Append("    docker ps --format '{{.Names}}' | grep -q '^keycloak-' || break");
        script.Append("    sleep 1");
        script.Append("  done");
        script.Append("}");
        script.Append($"aspire start --no-build --non-interactive --apphost {quotedAppHost} {quotedExtraArguments}");
        script.Append("echo \"Press 'b' to rebuild and restart, or 'q' to stop the AppHost and exit.\"");

        // The trap drives the exit itself rather than just flagging the interrupt: a
        // SIGINT/SIGTERM arriving while `read`/`wait` is blocked doesn't reliably make
        // them return, so relying on the loop's own condition to notice it isn't safe.
        // TERM is trapped alongside INT - Docker/orchestrators send TERM first.
        script.Append("trap 'hj_stop; exit 0' INT TERM");
        script.Append("X_QUIT=0");
        script.Append("while IFS= read -rsn1 X_KEY; do");
        script.Append("  case \"$X_KEY\" in");
        script.Append("    q|Q) X_QUIT=1; break ;;");
        script.Append("    b|B)");
        script.Append("      trap - INT TERM");
        script.Append("      hj_stop");
        script.Append($"      {quotedCliBinary} {quotedBuildGlobalArguments} build");

        // The restart is already inside the utility container that owns this session.
        // Its Aspire CLI must run locally there; selecting a second container would
        // recreate the PID-1 lifecycle that this continuous script prevents.
        script.Append($"      exec {quotedCliBinary} {quotedRunGlobalArguments} run {quotedExtraArguments}");
        script.Append("      ;;");
        script.Append("  esac");
        script.Append("done");
        script.Append("if [ \"$X_QUIT\" = \"1\" ]; then");
        script.Append("  trap - INT TERM");
        script.Append("  hj_stop");
        script.Append("  exit 0");
        script.Append("fi");

        // `read` also returns non-zero on stdin EOF (e.g. `eshop run --agent < /dev/null`),
        // which would otherwise fall through and stop the AppHost like 'q' does - not a
        // quit request. Block without polling until INT/TERM instead; Docker kills this
        // whole process tree, including the backgrounded `tail`, the moment this
        // script's PID 1 exits, so no separate cleanup of it is needed.
        script.Append("tail -f /dev/null &");
        script.Append("wait $!");

        return script.Build();
    }
}
