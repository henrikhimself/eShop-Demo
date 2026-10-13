// <copyright file="SchemaMarkerStore.cs" company="Henrik Jensen">
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

using Hj.EShop.Migrations.Common;
using Microsoft.Data.SqlClient;

namespace Hj.EShop.Migrations.Orchestration;

public static class SchemaMarkerStore
{
    private const string TableName = "SchemaMigrationMarkers";

    public static Task EnsureTableExistsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        return connection.ExecuteNonQueryAsync(
            $"""
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '{TableName}')
            BEGIN
                CREATE TABLE {TableName} (
                    Component NVARCHAR(200) NOT NULL PRIMARY KEY,
                    SchemaVersion NVARCHAR(200) NOT NULL,
                    CompletedAtUtc DATETIME2 NOT NULL,
                    Succeeded BIT NOT NULL,
                    FailureMessage NVARCHAR(MAX) NULL
                );
            END
            """,
            transaction: null,
            configureParameters: null,
            cancellationToken);
    }

    public static Task<SchemaMarker?> GetLatestAsync(SqlConnection connection, string component, CancellationToken cancellationToken)
    {
        return connection.ExecuteReaderAsync(
            $"SELECT SchemaVersion, CompletedAtUtc, Succeeded, FailureMessage FROM {TableName} WHERE Component = @component",
            transaction: null,
            configureParameters: parameters => parameters.AddWithValue("@component", component),
            readResult: async (reader, ct) =>
            {
                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                return new SchemaMarker(
                    component,
                    reader.GetString(0),
                    reader.GetDateTime(1),
                    reader.GetBoolean(2),
                    reader.GetNullableString(3));
            },
            cancellationToken);
    }

    // Upsert - only the latest attempt is kept per component, not a history.
    public static Task WriteAsync(SqlConnection connection, SchemaMarker marker, CancellationToken cancellationToken)
    {
        return connection.ExecuteNonQueryAsync(
            $"""
            MERGE {TableName} AS target
            USING (SELECT @component AS Component) AS source
            ON target.Component = source.Component
            WHEN MATCHED THEN
                UPDATE SET SchemaVersion = @schemaVersion, CompletedAtUtc = @completedAtUtc, Succeeded = @succeeded, FailureMessage = @failureMessage
            WHEN NOT MATCHED THEN
                INSERT (Component, SchemaVersion, CompletedAtUtc, Succeeded, FailureMessage)
                VALUES (@component, @schemaVersion, @completedAtUtc, @succeeded, @failureMessage);
            """,
            transaction: null,
            configureParameters: parameters =>
            {
                parameters.AddWithValue("@component", marker.Component);
                parameters.AddWithValue("@schemaVersion", marker.SchemaVersion);
                parameters.AddWithValue("@completedAtUtc", marker.CompletedAtUtc);
                parameters.AddWithValue("@succeeded", marker.Succeeded);
                parameters.AddWithValue("@failureMessage", marker.FailureMessage.DbNullIfNull());
            },
            cancellationToken);
    }
}
