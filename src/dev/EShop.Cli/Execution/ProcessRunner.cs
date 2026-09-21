// <copyright file="ProcessRunner.cs" company="Henrik Jensen">
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

using System.Diagnostics;
using System.Text;

namespace Hj.EShop.Cli.Execution;

internal sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        bool captureOutput = !request.Interactive;
        ProcessStartInfo startInfo = new(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            UseShellExecute = false,
        };

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.EnvironmentVariables is not null)
        {
            foreach (KeyValuePair<string, string> variable in request.EnvironmentVariables)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        using Process process = new() { StartInfo = startInfo };
        StringBuilder standardOutput = new();
        StringBuilder standardError = new();

        process.Start();

        // Killed on cancellation, not just disposed - Process.Dispose() releases the
        // .NET wrapper only, not the OS process or its children. Known gap: for a
        // `docker run` child (ContainerRunner) this kills the client process, not the
        // container itself.
        using CancellationTokenRegistration killRegistration = cancellationToken.Register(
            () => KillProcessTree(process));

        if (captureOutput)
        {
            Task standardOutputTask = ReadOutputAsync(
                process.StandardOutput, ProcessOutputStream.StandardOutput, standardOutput, request.OutputLineHandler);
            Task standardErrorTask = ReadOutputAsync(
                process.StandardError, ProcessOutputStream.StandardError, standardError, request.OutputLineHandler);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await process.WaitForExitAsync(CancellationToken.None);
                throw;
            }

            await Task.WhenAll(standardOutputTask, standardErrorTask);
            return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private static async Task ReadOutputAsync(
        StreamReader reader,
        ProcessOutputStream stream,
        StringBuilder output,
        Action<ProcessOutputLine>? outputLineHandler)
    {
        while (await reader.ReadLineAsync() is string line)
        {
            output.AppendLine(line);
            outputLineHandler?.Invoke(new ProcessOutputLine(stream, line));
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Exited between the check above and Kill - already gone.
        }
    }
}
