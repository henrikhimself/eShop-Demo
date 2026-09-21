// <copyright file="SqlCommandExtensions.cs" company="Henrik Jensen">
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

using System.Data;
using Microsoft.Data.SqlClient;

namespace Hj.EShop.Migrations.Common;

// Collapses the SqlCommand create/configure/execute ceremony shared by EShop.Migrations.Optimizely and EShop.Migrations.Orchestration.
// Deliberately excludes SchemaMigrationLock.TryAcquireAsync, which needs the command alive after execution to read back an output ReturnValue parameter.
public static class SqlCommandExtensions
{
    public static async Task<int> ExecuteNonQueryAsync(
        this SqlConnection connection,
        string commandText,
        SqlTransaction? transaction,
        Action<SqlParameterCollection>? configureParameters,
        CancellationToken cancellationToken,
        CommandType commandType = CommandType.Text)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.CommandType = commandType;
        configureParameters?.Invoke(command.Parameters);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Takes a mapping delegate rather than returning the open SqlDataReader itself, so
    // the command and reader can both be disposed inside this method (row mapping is
    // inherently different per call site, but resource cleanup isn't).
    public static async Task<TResult> ExecuteReaderAsync<TResult>(
        this SqlConnection connection,
        string commandText,
        SqlTransaction? transaction,
        Action<SqlParameterCollection>? configureParameters,
        Func<SqlDataReader, CancellationToken, Task<TResult>> readResult,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        configureParameters?.Invoke(command.Parameters);

        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await readResult(reader, cancellationToken);
    }
}
