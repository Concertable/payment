using Concertable.Payment.Contracts;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.IntegrationTests;

public sealed class FinancialOperationPersistenceTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public FinancialOperationPersistenceTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task InitialMigration_FinancialOperation_PersistsRecoveryState()
    {
        var id = Guid.NewGuid();
        var reference = new PaymentOperationReference("escrow", "order:17");
        var completedAt = DateTimeOffset.UtcNow;
        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();
            var operation = FinancialOperationEntity.Create(
                id,
                reference,
                new string('A', 64),
                completedAt.AddMinutes(-1));
            operation.Succeed("pi_test", completedAt);
            context.FinancialOperations.Add(operation);
            await context.SaveChangesAsync();
        }

        await using var verification = CreateContext();
        var persisted = await verification.FinancialOperations.SingleAsync(value => value.Id == id);

        Assert.Equal(reference.OperationType, persisted.OperationType);
        Assert.Equal(reference.ClientReference, persisted.ClientReference);
        Assert.Equal(FinancialOperationStatus.Succeeded, persisted.Status);
        Assert.Equal("pi_test", persisted.ReferenceId);
        Assert.Equal(completedAt, persisted.CompletedAt);
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
