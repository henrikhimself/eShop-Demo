// <copyright file="SqlScriptBatchSplitter.cs" company="Henrik Jensen">
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

using System.Text;

namespace Hj.EShop.Migrations.Optimizely;

// ADO.NET's SqlCommand has no native batch-separator support, so this splits a T-SQL script into batches on lines that are exactly "GO" (case-insensitive).
// Kept as a pure function, independent of SqlCommand/SqlConnection, so it's unit-testable without a real database.
public static class SqlScriptBatchSplitter
{
    public static IEnumerable<string> Split(TextReader reader)
    {
        StringBuilder batch = new();
        string? line = reader.ReadLine();
        while (line is not null)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (batch.Length > 0)
                {
                    yield return batch.ToString();
                    batch.Clear();
                }
            }
            else if (line.Length > 0)
            {
                batch.Append(line).Append("\r\n");
            }

            line = reader.ReadLine();
        }

        if (batch.Length > 0)
        {
            yield return batch.ToString();
        }
    }
}
