using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.IntegrationTests;

public sealed class CommissionBindingPersistenceTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public CommissionBindingPersistenceTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task Binding_PersistsConfigurationReferenceWithoutConfigurationTerms()
    {
        await using (var migrate = CreateContext())
            await migrate.Database.MigrateAsync();

        var configurationId = Guid.NewGuid();
        Guid bindingId;

        await using (var context = CreateContext())
        {
            var binding = CommissionBindingEntity.Create(
                configurationId,
                $"booking:{Guid.NewGuid():N}",
                $"payer:{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow);
            context.CommissionBindings.Add(binding);
            await context.SaveChangesAsync();
            bindingId = binding.Id;
        }

        await using var verification = CreateContext();
        var loaded = await new CommissionBindingRepository(verification).GetByIdAsync(bindingId);

        Assert.NotNull(loaded);
        Assert.Equal(configurationId, loaded.CommissionConfigurationId);
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
