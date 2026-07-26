using Concertable.Payment.Application.Interfaces;
using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure.Repositories;

internal sealed class LedgerTransactionRepository : Repository<LedgerTransactionEntity>, ILedgerTransactionRepository
{
    private new readonly PaymentDbContext context;

    public LedgerTransactionRepository(PaymentDbContext context)
        : base(context)
    {
        this.context = context;
    }

    public async Task<bool> CommitPostingAsync(CancellationToken ct = default)
    {
        while (true)
        {
            try
            {
                await context.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex) when (
                ex.IsDuplicateKey() &&
                ex.InnerException!.Message.Contains(
                    LedgerTransactionEntityConfiguration.PostingIdentityIndex,
                    StringComparison.Ordinal))
            {
                context.ChangeTracker.Clear();
                return false;
            }
            catch (DbUpdateException ex) when (
                ex.IsDuplicateKey() &&
                ex.InnerException!.Message.Contains(
                    LedgerAccountEntityConfiguration.IdentityIndex,
                    StringComparison.Ordinal))
            {
                if (!await ReconcileConcurrentAccountsAsync(ct))
                    throw;
            }
        }
    }

    private async Task<bool> ReconcileConcurrentAccountsAsync(CancellationToken ct)
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
                    ct);

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
