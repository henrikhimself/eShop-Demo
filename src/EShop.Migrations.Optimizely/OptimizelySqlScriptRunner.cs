// <copyright file="OptimizelySqlScriptRunner.cs" company="Henrik Jensen">
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

using System.Globalization;
using Hj.EShop.Migrations.Common;
using Microsoft.Data.SqlClient;

namespace Hj.EShop.Migrations.Optimizely;

// Runs a sorted set of Optimizely schema scripts (baseline plus incremental) against an
// open connection, in one transaction for the whole batch - reimplements
// EPiServer.Net.Cli's ScriptRunner/ScriptValidatorParser algorithm (confirmed by
// decompiling it with ilspycmd) in raw ADO.NET, rather than shelling out to that tool.
// See doc/adr/0023-explicit-database-schema-migration-resources.md and doc/CHRONICLE.md.
public static class OptimizelySqlScriptRunner
{
    // Each script's own validating query decides whether it runs, is skipped (already
    // applied), or aborts the whole batch (unexpected database state) - see
    // SqlScriptValidatingQueryParser and SqlStatusCode. Every file in `files` MUST have a
    // validating-query block; a script without one is an error, not "run unconditionally".
    public static async Task ExecuteAsync(
        SqlConnection connection, IReadOnlyList<FileInfo> files, CancellationToken cancellationToken)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (FileInfo file in files)
            {
                await ExecuteFileAsync(connection, transaction, file, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task ExecuteFileAsync(
        SqlConnection connection, SqlTransaction transaction, FileInfo file, CancellationToken cancellationToken)
    {
        using StreamReader reader = file.OpenText();
        string validatingQuery = SqlScriptValidatingQueryParser.GetValidationQuery(reader)
            ?? throw new InvalidOperationException($"Missing validating-query block in script '{file.Name}'.");

        SqlValidationStatus status = await GetValidationStatusAsync(connection, transaction, validatingQuery, cancellationToken);
        switch (status.StatusCode)
        {
            case SqlStatusCode.AlreadyIn:
                return;
            case SqlStatusCode.Valid:
                await ExecuteScriptBodyAsync(connection, transaction, reader, cancellationToken);
                return;
            default:
                throw new InvalidOperationException(
                    $"Validation failed for script '{file.Name}': {status.StatusMessage ?? status.StatusCode.ToString()}");
        }
    }

    private static Task<SqlValidationStatus> GetValidationStatusAsync(
        SqlConnection connection, SqlTransaction transaction, string query, CancellationToken cancellationToken)
    {
        return connection.ExecuteReaderAsync(
            query,
            transaction,
            configureParameters: null,
            readResult: async (reader, ct) =>
            {
                if (!await reader.ReadAsync(ct))
                {
                    throw new InvalidOperationException("The validating query did not return any rows.");
                }

                int rawStatusCode = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                SqlStatusCode statusCode = Enum.IsDefined(typeof(SqlStatusCode), rawStatusCode)
                    ? (SqlStatusCode)rawStatusCode
                    : SqlStatusCode.Undefined;
                return new SqlValidationStatus(statusCode, reader.GetNullableString(1));
            },
            cancellationToken);
    }

    // Batch splitting itself is pulled out into SqlScriptBatchSplitter (a pure function,
    // unit-testable without a database) - starts reading from exactly where the
    // validating-query parser left off.
    private static async Task ExecuteScriptBodyAsync(
        SqlConnection connection, SqlTransaction transaction, TextReader reader, CancellationToken cancellationToken)
    {
        foreach (string batch in SqlScriptBatchSplitter.Split(reader))
        {
            await connection.ExecuteNonQueryAsync(batch, transaction, configureParameters: null, cancellationToken);
        }
    }
}
