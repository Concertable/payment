using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Settings;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Concertable.Payment.IntegrationTests;

public sealed class CommissionAuthorizationClaimTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public CommissionAuthorizationClaimTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task DuplicateClaimForSameAuthorization_ViolatesUniqueIndex()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var authorization = await SeedAuthorizationAsync(context);

        context.CommissionAuthorizationClaims.Add(CommissionAuthorizationClaimEntity.Create(
            authorization.Id, CommissionAuthorizationConsumer.Escrow, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();

        context.CommissionAuthorizationClaims.Add(CommissionAuthorizationClaimEntity.Create(
            authorization.Id, CommissionAuthorizationConsumer.Settlement, DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ClaimAuthorizationAsync_SecondConsumer_FailsClosedWhileFirstAndIdempotentRetrySucceed()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        var authorization = await SeedAuthorizationAsync(context);
        context.ChangeTracker.Clear();
        var service = CreateService(context);

        var escrowClaim = await service.ClaimAuthorizationAsync(
            authorization.Id, CommissionAuthorizationConsumer.Escrow);
        Assert.True(escrowClaim.IsSuccess);

        var settlementClaim = await service.ClaimAuthorizationAsync(
            authorization.Id, CommissionAuthorizationConsumer.Settlement);
        Assert.True(settlementClaim.IsFailed);
        Assert.Contains(settlementClaim.Errors, e => e.Message == "commission_authorization_already_consumed");

        var escrowRetry = await service.ClaimAuthorizationAsync(
            authorization.Id, CommissionAuthorizationConsumer.Escrow);
        Assert.True(escrowRetry.IsSuccess);

        context.ChangeTracker.Clear();
        var claim = await context.CommissionAuthorizationClaims
            .SingleAsync(c => c.CommissionAuthorizationId == authorization.Id);
        Assert.Equal(CommissionAuthorizationConsumer.Escrow, claim.Consumer);
    }

    private static async Task<CommissionAuthorizationEntity> SeedAuthorizationAsync(PaymentDbContext context)
    {
        var configuration = CommissionConfigurationEntity.Create(
            Guid.NewGuid(), $"integration-{Guid.NewGuid():N}", Currency.Gbp, 500, DateTimeOffset.UtcNow);
        var authorization = CommissionAuthorizationEntity.Create(
            configuration.Id, $"booking:{Guid.NewGuid():N}", $"payer:{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        context.AddRange(configuration, authorization);
        await context.SaveChangesAsync();
        return authorization;
    }

    private static CommissionService CreateService(PaymentDbContext context) =>
        new(
            new CommissionConfigurationRepository(context),
            new CommissionAuthorizationRepository(context),
            new CommissionAuthorizationClaimRepository(context),
            context,
            new UnitOfWork(context),
            new CommissionCalculator(),
            Options.Create(new PlatformCommissionOptions
            {
                ConfigurationId = Guid.NewGuid(),
                Version = "integration",
                Currency = "GBP",
                RateBasisPoints = 500
            }),
            Options.Create(new PlatformCommissionTaxOptions { VatRateBasisPoints = 2000 }),
            TimeProvider.System);

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
