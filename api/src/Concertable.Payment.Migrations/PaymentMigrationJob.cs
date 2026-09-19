using Concertable.Messaging.Infrastructure.Inbox;
using Concertable.Messaging.Infrastructure.Outbox;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Migrations;

internal static class PaymentMigrationJob
{
    public static async Task RunAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var contexts = new Func<DbContext>[]
        {
            () => new PaymentDbContext(
                new DbContextOptionsBuilder<PaymentDbContext>()
                    .UseNpgsql(
                        connectionString,
                        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema.Name))
                    .Options,
                Options.Create(new OutboxOptions()),
                new PaymentConfigurationProvider()),
            () => new InboxDbContext(
                new DbContextOptionsBuilder<InboxDbContext>()
                    .UseNpgsql(
                        connectionString,
                        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Inbox", Schema.Messaging))
                    .Options),
            () => new OutboxDbContext(
                new DbContextOptionsBuilder<OutboxDbContext>()
                    .UseNpgsql(
                        connectionString,
                        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Outbox", Schema.Messaging))
                    .Options,
                Options.Create(new OutboxOptions())),
        };

        foreach (var createContext in contexts)
        {
            await using var context = createContext();
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            if ((await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
                throw new InvalidOperationException($"Migrations remain pending for {context.GetType().Name}.");
        }
    }
}
