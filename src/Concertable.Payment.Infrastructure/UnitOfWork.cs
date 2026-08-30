using Concertable.DataAccess.Application;
using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Concertable.Payment.Infrastructure;

internal interface IUnitOfWork : IUnitOfWork<PaymentDbContext>;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly PaymentDbContext context;

    public UnitOfWork(PaymentDbContext context)
    {
        this.context = context;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesWithAccountReconciliationAsync(cancellationToken);

    public async Task<bool> TrySaveChangesAsync(
        Func<DbUpdateException, bool> isExpected,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesWithAccountReconciliationAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (isExpected(exception))
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        context.Database.BeginTransactionAsync(cancellationToken);

    public Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, cancellationToken);

    public Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            var result = await operation();
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });

    private async Task SaveChangesWithAccountReconciliationAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception) when (
                exception.IsDuplicateKey() &&
                exception.InnerException?.Message.Contains(
                    LedgerAccountEntityConfiguration.IdentityIndex,
                    StringComparison.Ordinal) == true)
            {
                if (!await ReconcileConcurrentAccountsAsync(cancellationToken))
                    throw;
            }
        }
    }

    private async Task<bool> ReconcileConcurrentAccountsAsync(CancellationToken cancellationToken)
    {
        var addedAccounts = context.ChangeTracker
            .Entries<LedgerAccountEntity>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();
        var reconciled = false;

        foreach (var addedAccount in addedAccounts)
        {
            var winner = await context.LedgerAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    account =>
                        account.Type == addedAccount.Type &&
                        account.OwnerId == addedAccount.OwnerId &&
                        account.Currency == addedAccount.Currency,
                    cancellationToken);

            if (winner is null)
                continue;

            var dependentEntries = context.ChangeTracker
                .Entries<LedgerEntryEntity>()
                .Where(entry => ReferenceEquals(entry.Entity.Account, addedAccount))
                .ToList();

            context.Attach(winner);

            foreach (var dependentEntry in dependentEntries)
                dependentEntry.Reference(entry => entry.Account).CurrentValue = winner;

            context.Entry(addedAccount).State = EntityState.Detached;
            reconciled = true;
        }

        return reconciled;
    }
}
