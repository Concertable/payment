using Concertable.Messaging.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.Infrastructure.Data;

internal sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(
                DesignTimeConfiguration.ConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema.Name))
            .Options;
        return new PaymentDbContext(
            options,
            Options.Create(new OutboxOptions()),
            new PaymentConfigurationProvider());
    }
}
