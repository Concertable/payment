using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Enums;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.IntegrationTests;

public sealed class UnitOfWorkTransactionTests : IClassFixture<SqlFixture>
{
    private static int referenceId = 10_000;
    private readonly SqlFixture sql;

    public UnitOfWorkTransactionTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task SaveChangesAsync_CommitsOperationalStateAndLedgerRowsTogether()
    {
        await using var context = await CreateMigratedContextAsync();
        var escrow = await AddPendingEscrowAsync(context);
        var unitOfWork = new UnitOfWork(context);
        var ledger = CreateLedger(context);

        escrow.Confirm();
        await ledger.StageAsync(
            LedgerPostings.EscrowHold(
                escrow.FromOwnerId,
                escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                new(escrow.OperationType, escrow.ClientReference),
                escrow.ChargeId));
        await unitOfWork.SaveChangesAsync();

        await using var verificationContext = CreateContext();
        var persistedEscrow = await verificationContext.Escrows.SingleAsync(e => e.Id == escrow.Id);
        var transaction = await verificationContext.LedgerTransactions
            .Include(t => t.Entries)
            .SingleAsync(t => t.ExternalId == escrow.ChargeId);

        Assert.Equal(EscrowStatus.Held, persistedEscrow.Status);
        Assert.Equal(2, transaction.Entries.Count);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenLedgerStagingFails_LeavesOperationalStateAndLedgerRowsUnchanged()
    {
        await using var context = await CreateMigratedContextAsync();
        var escrow = await AddPendingEscrowAsync(context);
        var initialAccountCount = await context.LedgerAccounts.CountAsync();
        var initialTransactionCount = await context.LedgerTransactions.CountAsync();
        var initialEntryCount = await context.LedgerEntries.CountAsync();
        var ledger = CreateLedger(context);

        var unbalancedPosting = new LedgerPosting(
            LedgerPostingType.EscrowHold,
            escrow.ChargeId,
            new(escrow.OperationType, escrow.ClientReference),
            escrow.ChargeId,
            [
                new(
                    new(LedgerAccountType.Receivable, escrow.FromOwnerId),
                    LedgerDirection.Debit,
                    escrow.PayerTotalMinor.ToMoney(escrow.Currency)),
                new(
                    new(LedgerAccountType.StripeClearing, null),
                    LedgerDirection.Credit,
                    Money.Gbp(1))
            ]);

        escrow.Confirm();
        await Assert.ThrowsAsync<DomainException>(() => ledger.StageAsync(unbalancedPosting));

        await using var verificationContext = CreateContext();
        Assert.Equal(
            EscrowStatus.Pending,
            await verificationContext.Escrows
                .Where(e => e.Id == escrow.Id)
                .Select(e => e.Status)
                .SingleAsync());
        Assert.Equal(initialAccountCount, await verificationContext.LedgerAccounts.CountAsync());
        Assert.Equal(initialTransactionCount, await verificationContext.LedgerTransactions.CountAsync());
        Assert.Equal(initialEntryCount, await verificationContext.LedgerEntries.CountAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_WhenSaveFails_RollsBackOperationalStateAndLedgerRows()
    {
        await using var context = await CreateMigratedContextAsync();
        var duplicateExternalId = $"pi_{Guid.NewGuid():N}";
        var payerId = Guid.NewGuid();
        var ledger = CreateLedger(context);
        var unitOfWork = new UnitOfWork(context);

        await ledger.StageAsync(
            LedgerPostings.EscrowHold(
                payerId,
                Money.Gbp(50),
                NextReference(),
                duplicateExternalId));
        await unitOfWork.SaveChangesAsync();

        var escrow = await AddPendingEscrowAsync(context, payerId);
        var initialAccountCount = await context.LedgerAccounts.CountAsync();
        var initialTransactionCount = await context.LedgerTransactions.CountAsync();
        var initialEntryCount = await context.LedgerEntries.CountAsync();

        escrow.Confirm();
        await ledger.StageAsync(
            LedgerPostings.EscrowHold(
                escrow.FromOwnerId,
                escrow.PayerTotalMinor.ToMoney(escrow.Currency),
                new(escrow.OperationType, escrow.ClientReference),
                duplicateExternalId));

        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());

        await using var verificationContext = CreateContext();
        Assert.Equal(
            EscrowStatus.Pending,
            await verificationContext.Escrows
                .Where(e => e.Id == escrow.Id)
                .Select(e => e.Status)
                .SingleAsync());
        Assert.Equal(initialAccountCount, await verificationContext.LedgerAccounts.CountAsync());
        Assert.Equal(initialTransactionCount, await verificationContext.LedgerTransactions.CountAsync());
        Assert.Equal(initialEntryCount, await verificationContext.LedgerEntries.CountAsync());
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

    private static LedgerService CreateLedger(PaymentDbContext context) =>
        new(
            new LedgerAccountRepository(context),
            new LedgerTransactionRepository(context),
            TimeProvider.System);

    private static async Task<EscrowEntity> AddPendingEscrowAsync(
        PaymentDbContext context,
        Guid? payerId = null)
    {
        var escrow = EscrowEntity.Create(
            NextReference(),
            payerId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Money.Gbp(50),
            Money.Gbp(0),
            $"pi_{Guid.NewGuid():N}");
        escrow.CreatedAt = DateTimeOffset.UtcNow;
        escrow.CreatedBy = "integration-test";

        context.Escrows.Add(escrow);
        await context.SaveChangesAsync();
        return escrow;
    }

    private static PaymentOperationReference NextReference() =>
        new("escrow", $"order:{Interlocked.Increment(ref referenceId)}");
}
