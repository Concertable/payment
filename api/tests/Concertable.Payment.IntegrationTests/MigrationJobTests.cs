extern alias PaymentMigrations;

using Concertable.Testing.Integration;
using Npgsql;
using PaymentMigrationJob = PaymentMigrations::Concertable.Payment.Migrations.PaymentMigrationJob;

namespace Concertable.Payment.IntegrationTests;

public sealed class MigrationJobTests
{
    [Fact]
    public async Task RunAsync_CanMigrateCleanDatabaseTwice()
    {
        var postgres = new PostgresFixture();
        await postgres.InitializeAsync();
        try
        {
            await PaymentMigrationJob.RunAsync(postgres.ConnectionString);
            await PaymentMigrationJob.RunAsync(postgres.ConnectionString);

            await using var connection = new NpgsqlConnection(postgres.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT table_schema || '.' || table_name
                FROM information_schema.tables
                WHERE table_name LIKE '__EFMigrationsHistory%'
                ORDER BY table_schema, table_name
                """;
            await using var reader = await command.ExecuteReaderAsync();
            var histories = new List<string>();
            while (await reader.ReadAsync())
                histories.Add(reader.GetString(0));

            Assert.Equal(
            [
                "messaging.__EFMigrationsHistory_Inbox",
                "messaging.__EFMigrationsHistory_Outbox",
                "payment.__EFMigrationsHistory",
            ],
            histories);
        }
        finally
        {
            await postgres.DisposeAsync();
        }
    }
}
