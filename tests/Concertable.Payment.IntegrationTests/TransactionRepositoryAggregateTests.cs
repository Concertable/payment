using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.IntegrationTests;

public sealed class TransactionRepositoryAggregateTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public TransactionRepositoryAggregateTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task GetCompletedTicketRevenueAsync_FiltersByPayeeStatusAndPeriod()
    {
        await using var context = await CreateMigratedContextAsync();
        var payeeId = Guid.NewGuid();
        var otherPayeeId = Guid.NewGuid();
        var monthStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var period = new DateRange(monthStart, monthStart.AddMonths(1));

        context.AddRange(
            Ticket(payeeId, 1200, TransactionStatus.Complete, monthStart),
            Ticket(payeeId, 800, TransactionStatus.Complete, monthStart.AddDays(12)),
            Ticket(payeeId, 900, TransactionStatus.Complete, monthStart.AddTicks(-1)),
            Ticket(payeeId, 500, TransactionStatus.Complete, period.End),
            Ticket(payeeId, 700, TransactionStatus.Pending, monthStart.AddDays(1)),
            Ticket(otherPayeeId, 600, TransactionStatus.Complete, monthStart.AddDays(1)));
        await context.SaveChangesAsync();

        var amount = await new TransactionRepository(context)
            .GetCompletedTicketRevenueAsync(payeeId, period);

        Assert.Equal(2000, amount);
    }

    [Fact]
    public async Task GetCompletedSettlementPayoutsAsync_SumsPayeeGrossOnly()
    {
        await using var context = await CreateMigratedContextAsync();
        var payeeId = Guid.NewGuid();
        var otherPayeeId = Guid.NewGuid();
        var monthStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var period = new DateRange(monthStart, monthStart.AddMonths(1));

        context.AddRange(
            Settlement(payeeId, 2500, 500, TransactionStatus.Complete, monthStart),
            Settlement(payeeId, 1500, 300, TransactionStatus.Complete, monthStart.AddDays(12)),
            Settlement(payeeId, 900, 100, TransactionStatus.Complete, monthStart.AddTicks(-1)),
            Settlement(payeeId, 800, 100, TransactionStatus.Complete, period.End),
            Settlement(payeeId, 700, 100, TransactionStatus.Pending, monthStart.AddDays(1)),
            Settlement(otherPayeeId, 600, 100, TransactionStatus.Complete, monthStart.AddDays(1)));
        await context.SaveChangesAsync();

        var amount = await new TransactionRepository(context)
            .GetCompletedSettlementPayoutsAsync(payeeId, period);

        Assert.Equal(3200, amount);
    }

    private static TicketTransactionEntity Ticket(
        Guid payeeId,
        long amount,
        TransactionStatus status,
        DateTime createdAt)
    {
        var transaction = TicketTransactionEntity.Create(
            Guid.NewGuid(),
            payeeId,
            $"pi_{Guid.NewGuid():N}",
            amount,
            status,
            Random.Shared.Next());
        transaction.CreatedAt = createdAt;
        transaction.CreatedBy = "test";
        return transaction;
    }

    private static SettlementTransactionEntity Settlement(
        Guid payeeId,
        long amount,
        long fee,
        TransactionStatus status,
        DateTime createdAt)
    {
        var transaction = SettlementTransactionEntity.Create(
            Guid.NewGuid(),
            payeeId,
            $"pi_{Guid.NewGuid():N}",
            amount,
            fee,
            status,
            Random.Shared.Next());
        transaction.CreatedAt = createdAt;
        transaction.CreatedBy = "test";
        return transaction;
    }

    private async Task<PaymentDbContext> CreateMigratedContextAsync()
    {
        var context = CreateContext();
        await context.Database.MigrateAsync();
        return context;
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }
}
