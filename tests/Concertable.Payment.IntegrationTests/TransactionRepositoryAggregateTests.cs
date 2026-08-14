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
    public async Task GetCompletedSettlementPayoutsAsync_UsesCompletionPeriodAndSumsPayeeGrossOnly()
    {
        await using var context = await CreateMigratedContextAsync();
        var payeeId = Guid.NewGuid();
        var otherPayeeId = Guid.NewGuid();
        var monthStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var period = new DateRange(monthStart, monthStart.AddMonths(1));

        context.AddRange(
            Settlement(
                payeeId,
                2500,
                500,
                TransactionStatus.Complete,
                monthStart.AddMonths(-1),
                completedAt: monthStart),
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

    [Fact]
    public async Task GetCompletedTicketRevenueByMonthAsync_GroupsAndOrdersCompletedPayeeTransactions()
    {
        await using var context = await CreateMigratedContextAsync();
        var payeeId = Guid.NewGuid();
        var period = new DateRange(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        context.AddRange(
            Ticket(payeeId, 700, TransactionStatus.Complete, new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc)),
            Ticket(payeeId, 1200, TransactionStatus.Complete, new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc)),
            Ticket(payeeId, 800, TransactionStatus.Complete, new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc)),
            Ticket(payeeId, 500, TransactionStatus.Pending, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
            Ticket(Guid.NewGuid(), 900, TransactionStatus.Complete, new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();

        var points = await new TransactionRepository(context)
            .GetCompletedTicketRevenueByMonthAsync(payeeId, period);

        Assert.Collection(
            points,
            point => Assert.Equal(
                new(new DateOnly(2026, 6, 1), 2000, 2000, 2),
                point),
            point => Assert.Equal(
                new(new DateOnly(2026, 8, 1), 700, 700, 1),
                point));
    }

    [Fact]
    public async Task GetCompletedSettlementPayoutsByMonthAsync_UsesPayeeGross()
    {
        await using var context = await CreateMigratedContextAsync();
        var payeeId = Guid.NewGuid();
        var period = new DateRange(
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        context.AddRange(
            Settlement(
                payeeId,
                2500,
                500,
                TransactionStatus.Complete,
                new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                completedAt: new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc)),
            Settlement(payeeId, 1500, 300, TransactionStatus.Complete, new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc)),
            Settlement(payeeId, 900, 100, TransactionStatus.Complete, new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)),
            Settlement(payeeId, 700, 100, TransactionStatus.Failed, new DateTime(2026, 8, 4, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();

        var points = await new TransactionRepository(context)
            .GetCompletedSettlementPayoutsByMonthAsync(payeeId, period);

        Assert.Collection(
            points,
            point => Assert.Equal(
                new(new DateOnly(2026, 7, 1), 3200, 3200, 2),
                point),
            point => Assert.Equal(
                new(new DateOnly(2026, 8, 1), 800, 800, 1),
                point));
    }

    [Fact]
    public async Task GetRecentCompletedSettlementsAsync_FiltersEitherSideAndTakesNewest()
    {
        await using var context = await CreateMigratedContextAsync();
        var ownerId = Guid.NewGuid();

        context.AddRange(
            Settlement(Guid.NewGuid(), 1000, 100, TransactionStatus.Complete,
                new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc), ownerId, 101,
                new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)),
            Settlement(ownerId, 2000, 200, TransactionStatus.Complete,
                new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), 102,
                new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc)),
            Settlement(ownerId, 3000, 300, TransactionStatus.Complete,
                new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), 103),
            Settlement(ownerId, 4000, 400, TransactionStatus.Pending,
                new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), 104),
            Settlement(Guid.NewGuid(), 5000, 500, TransactionStatus.Complete,
                new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), 105));
        await context.SaveChangesAsync();

        var settlements = await new TransactionRepository(context)
            .GetRecentCompletedSettlementsAsync(ownerId, 2);

        Assert.Equal([102, 103], settlements.Select(s => s.BookingId));
        Assert.Equal([1800L, 2700L], settlements.Select(s => s.AmountMinor));
        Assert.Equal(
            [
                new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)
            ],
            settlements.Select(s => s.At));
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
        DateTime createdAt,
        Guid? payerId = null,
        int? bookingId = null,
        DateTime? completedAt = null)
    {
        var transaction = SettlementTransactionEntity.Create(
            payerId ?? Guid.NewGuid(),
            payeeId,
            $"pi_{Guid.NewGuid():N}",
            amount,
            fee,
            TransactionStatus.Pending,
            bookingId ?? Random.Shared.Next());
        if (status == TransactionStatus.Complete)
            transaction.Complete(completedAt ?? createdAt);
        else if (status == TransactionStatus.Failed)
            transaction.Fail();
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
