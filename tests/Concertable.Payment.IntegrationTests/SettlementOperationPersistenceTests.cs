using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.IntegrationTests;

public sealed class SettlementOperationPersistenceTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public SettlementOperationPersistenceTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task OperationReplayState_RoundTripsForSettlementAndEscrow()
    {
        await using (var migration = CreateContext())
            await migration.Database.MigrateAsync();

        var chargeOperationId = Guid.CreateVersion7();
        var releaseOperationId = Guid.CreateVersion7();
        var payerId = Guid.NewGuid();
        var payeeId = Guid.NewGuid();
        var chargeFingerprint = SettlementOperationFingerprint.CreateCharge(
            chargeOperationId,
            payerId,
            payeeId,
            Money.Gbp(50),
            Money.Gbp(12),
            "pm_test",
            PaymentSession.OnSession,
            42);
        var settlement = SettlementTransactionEntity.CreateForOperation(
            payerId,
            payeeId,
            "pi_operation",
            6200,
            1200,
            TransactionStatus.Pending,
            42,
            chargeOperationId,
            chargeFingerprint,
            true,
            "secret_operation");
        var escrow = EscrowEntity.Create(
            43,
            payerId,
            payeeId,
            Money.Gbp(50),
            Money.Gbp(0),
            "pi_escrow");
        escrow.Confirm();

        await using (var seed = CreateContext())
        {
            seed.Add(settlement);
            seed.Add(escrow);
            await seed.SaveChangesAsync();

            var releaseFingerprint = SettlementOperationFingerprint.CreateRelease(releaseOperationId, escrow);
            escrow.BeginRelease(releaseOperationId, releaseFingerprint);
            await seed.SaveChangesAsync();
        }

        await using var verification = CreateContext();
        var storedSettlement = await verification.SettlementTransactions
            .SingleAsync(value => value.OperationId == chargeOperationId);
        var storedEscrow = await verification.Escrows
            .SingleAsync(value => value.ReleaseOperationId == releaseOperationId);

        Assert.Equal(chargeFingerprint.Version, storedSettlement.OperationFingerprintVersion);
        Assert.Equal(chargeFingerprint.Value, storedSettlement.OperationFingerprint);
        Assert.True(storedSettlement.RequiresAction);
        Assert.Equal("secret_operation", storedSettlement.ClientSecret);
        Assert.Equal(SettlementOperationFingerprint.CurrentVersion, storedEscrow.ReleaseOperationFingerprintVersion);
        Assert.NotNull(storedEscrow.ReleaseOperationFingerprint);
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
