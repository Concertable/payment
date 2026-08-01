using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.IntegrationTests;

public sealed class CommissionConfigurationPersistenceTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public CommissionConfigurationPersistenceTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task MultipleBindings_ReferenceOneImmutableConfiguration()
    {
        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        var configurationId = Guid.NewGuid();
        var version = $"integration-{Guid.NewGuid():N}";
        Guid firstBindingId;

        await using (var context = CreateContext())
        {
            var configurations = new CommissionConfigurationRepository(context);
            var configuration = await configurations.GetOrCreateAsync(
                CommissionConfigurationEntity.Create(
                    configurationId,
                    version,
                    Currency.Gbp,
                    500,
                    DateTimeOffset.UtcNow));
            var first = CommissionBindingEntity.Create(
                configuration, $"booking:{Guid.NewGuid():N}", $"payer:{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
            var second = CommissionBindingEntity.Create(
                configuration, $"booking:{Guid.NewGuid():N}", $"payer:{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
            context.CommissionBindings.AddRange(first, second);
            await context.SaveChangesAsync();
            firstBindingId = first.Id;
        }

        await using var verification = CreateContext();
        var loaded = await new CommissionBindingRepository(verification).GetByIdAsync(firstBindingId);

        Assert.NotNull(loaded);
        Assert.Equal(configurationId, loaded.CommissionConfigurationId);
        Assert.Equal(version, loaded.CommissionConfiguration.Version);
        Assert.Equal(500, loaded.Terms.RateBasisPoints);
        Assert.Equal(
            1,
            await verification.CommissionConfigurations.CountAsync(c => c.Id == configurationId));
        Assert.Equal(
            2,
            await verification.CommissionBindings.CountAsync(b => b.CommissionConfigurationId == configurationId));
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
