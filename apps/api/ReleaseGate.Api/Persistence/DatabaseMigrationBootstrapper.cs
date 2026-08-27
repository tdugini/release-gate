using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ReleaseGate.Api.Persistence;

public static class DatabaseMigrationBootstrapper
{
    public const string InitialMigrationId = "20260827090000_InitialSchema";
    private const int LegacyTableCount = 5;

    public static async Task PrepareLegacyDatabaseAsync(
        ReleaseGateDbContext db,
        CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var legacyTableCount = await CountLegacyTablesAsync(connection, cancellationToken);
            var hasMigrationHistory = await TableExistsAsync(
                connection,
                "__EFMigrationsHistory",
                cancellationToken);

            if (legacyTableCount == 0 || hasMigrationHistory)
            {
                return;
            }

            if (legacyTableCount != LegacyTableCount)
            {
                throw new InvalidOperationException(
                    "ReleaseGate found a partial legacy database schema without EF migration history. " +
                    "Refusing to baseline it automatically because the schema state is ambiguous.");
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );

                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ('20260827090000_InitialSchema', '10.0.4')
                ON CONFLICT ("MigrationId") DO NOTHING;
                """;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<int> CountLegacyTablesAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN (
                  'projects',
                  'environments',
                  'feature_flags',
                  'feature_flag_environments',
                  'flag_changes'
              );
            """;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name = @table_name
            );
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "table_name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }
}
