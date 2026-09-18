// <copyright file="SqlExceptionRetry.cs" company="Henrik Jensen">
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

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Hj.EShop.Migrations.Orchestration;

// See doc/CHRONICLE.md — retries any SqlException directly (not via an EF Core execution strategy) because not every cold-start failure is classified as transient, and non-EF runners have no execution strategy at all.
public static class SqlExceptionRetry
{
    public static async Task RunAsync(Func<Task> operation, int maxAttempts, ILogger logger)
    {
        int attempt = 1;
        while (true)
        {
            try
            {
                await operation();
                return;
            }
            catch (SqlException ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning(
                    ex,
                    "Attempt {Attempt}/{MaxAttempts} failed; retrying in {Delay}.",
                    attempt,
                    maxAttempts,
                    delay);
                await Task.Delay(delay);
                attempt++;
            }
        }
    }
}
