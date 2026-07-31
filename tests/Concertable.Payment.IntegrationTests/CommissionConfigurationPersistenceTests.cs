using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.IntegrationTests;

public sealed class CommissionConfigurationPersistenceTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public CommissionConfigurationPersistenceTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task Bootstrap_CompetingInstancesCreateOneSharedRevision()
    {
        await using (var migrationContext = CreateContext())
            await migrationContext.Database.MigrateAsync();

        var configurationId = Guid.NewGuid();
        var options = Options.Create(new PlatformCommissionOptions
        {
            ConfigurationId = configurationId,
            Version = $"integration-{Guid.NewGuid():N}",
            Currency = "GBP",
            RateBasisPoints = 500
        });
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = new CommissionConfigurationBootstrapper(firstContext, options, TimeProvider.System);
        var second = new CommissionConfigurationBootstrapper(secondContext, options, TimeProvider.System);

        await Task.WhenAll(
            first.EnsureConfiguredRevisionAsync(),
            second.EnsureConfiguredRevisionAsync());

        await using var verificationContext = CreateContext();
        var revision = await verificationContext.CommissionConfigurations
            .SingleAsync(configuration => configuration.Id == configurationId);
        Assert.Equal(500, revision.RateBasisPoints);
        Assert.Equal(Currency.Gbp, revision.Currency);
    }

    [Fact]
    public async Task Bootstrap_ExistingIdentityWithDifferentTermsFails()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var configurationId = Guid.NewGuid();
        var version = $"integration-{Guid.NewGuid():N}";
        context.CommissionConfigurations.Add(CommissionConfigurationEntity.Create(
            configurationId,
            version,
            Currency.Gbp,
            500,
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        var bootstrapper = new CommissionConfigurationBootstrapper(
            context,
            Options.Create(new PlatformCommissionOptions
            {
                ConfigurationId = configurationId,
                Version = version,
                Currency = "GBP",
                RateBasisPoints = 600
            }),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bootstrapper.EnsureConfiguredRevisionAsync());
    }

    [Fact]
    public async Task AuthorizationsShareConfigurationWithoutDuplicatingTerms()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var configuration = CommissionConfigurationEntity.Create(
            Guid.NewGuid(),
            $"integration-{Guid.NewGuid():N}",
            Currency.Gbp,
            500,
            DateTimeOffset.UtcNow);
        var first = CommissionBindingEntity.Create(
            configuration.Id,
            "booking:1",
            "payer:1",
            DateTimeOffset.UtcNow);
        var second = CommissionBindingEntity.Create(
            configuration.Id,
            "booking:2",
            "payer:2",
            DateTimeOffset.UtcNow);
        context.AddRange(configuration, first, second);

        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        var authorizations = await context.CommissionBindings
            .Include(binding => binding.CommissionConfiguration)
            .Where(binding => binding.CommissionConfigurationId == configuration.Id)
            .ToListAsync();
        Assert.Equal(2, authorizations.Count);
        Assert.All(authorizations, binding =>
            Assert.Same(
                authorizations[0].CommissionConfiguration,
                binding.CommissionConfiguration));
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
