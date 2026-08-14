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

// Shared by every migration runner (and the Bff, elsewhere): a bounded, number-agnostic
// safety net around a cold-starting SQL Server. EF Core's own execution strategy doesn't
// recognize every cold-start connection failure as transient (e.g. a pre-login handshake
// reset while the sql container is still starting up never reaches a retry), and a
// non-EF runner has no execution strategy at all - so this retries on any SqlException
// directly, independent of that classification. Rethrows once maxAttempts is exhausted,
// so a genuinely broken connection still fails loudly instead of retrying forever.
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
