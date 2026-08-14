// <copyright file="SchemaMigrationLock.cs" company="Henrik Jensen">
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
using Hj.EShop.Migrations.Common;
using Microsoft.Data.SqlClient;

namespace Hj.EShop.Migrations.Orchestration;

// Wraps sp_getapplock/sp_releaseapplock - the first raw ADO.NET in this repo, since
// EF Core's own ExecuteSql* has no way to read a stored procedure's return code. Uses
// LockOwner=Session, which ties the lock to the given connection's own SQL Server
// session - callers MUST acquire, run the protected operation, and release on the SAME
// open SqlConnection (e.g. the exact instance an EF Core DbContext.Database
// .GetDbConnection() returns, or a plain SqlConnection a non-EF runner opened directly),
// or the lock protects nothing: a different connection is a different session as far as
// sp_getapplock is concerned.
public static class SchemaMigrationLock
{
    // Returns false on timeout (lock held elsewhere) rather than throwing - callers
    // decide what "couldn't acquire" means for them (retry, fail the run, etc.).
    public static async Task<bool> TryAcquireAsync(
        SqlConnection connection, string lockName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@Resource", lockName);
        command.Parameters.AddWithValue("@LockMode", "Exclusive");
        command.Parameters.AddWithValue("@LockOwner", "Session");
        command.Parameters.AddWithValue("@LockTimeout", (int)timeout.TotalMilliseconds);
        SqlParameter returnValue = command.Parameters.Add("@ReturnValue", SqlDbType.Int);
        returnValue.Direction = ParameterDirection.ReturnValue;

        await command.ExecuteNonQueryAsync(cancellationToken);

        // sp_getapplock's return code: 0 or 1 means granted (immediately, or after a
        // wait), negative means a timeout, deadlock, or another failure.
        return (int)returnValue.Value >= 0;
    }

    public static Task ReleaseAsync(SqlConnection connection, string lockName, CancellationToken cancellationToken)
    {
        return connection.ExecuteNonQueryAsync(
            "sp_releaseapplock",
            transaction: null,
            configureParameters: parameters =>
            {
                parameters.AddWithValue("@Resource", lockName);
                parameters.AddWithValue("@LockOwner", "Session");
            },
            cancellationToken,
            CommandType.StoredProcedure);
    }
}
