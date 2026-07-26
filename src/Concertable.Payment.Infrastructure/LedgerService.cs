using Concertable.DataAccess.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.Infrastructure;

internal sealed class LedgerService : ILedgerService
{
    private readonly ILedgerAccountRepository accountRepository;
    private readonly ILedgerTransactionRepository transactionRepository;
    private readonly PaymentDbContext context;
    private readonly TimeProvider timeProvider;

    public LedgerService(
        ILedgerAccountRepository accountRepository,
        ILedgerTransactionRepository transactionRepository,
        PaymentDbContext context,
        TimeProvider timeProvider)
    {
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
        this.context = context;
        this.timeProvider = timeProvider;
    }

    public async Task PostAsync(LedgerPosting posting, CancellationToken ct = default)
    {
        var resolved = new Dictionary<LedgerAccountRef, LedgerAccountEntity>();
        var legs = new List<LedgerLeg>(posting.Legs.Count);

        foreach (var leg in posting.Legs)
        {
            var account = await ResolveAccountAsync(leg.Account, leg.Amount.Currency, resolved, ct);
            legs.Add(new LedgerLeg(account, leg.Direction, leg.Amount));
        }

        var transaction = LedgerTransactionEntity.Post(
            posting.PostingType,
            posting.ExternalId,
            posting.BookingId,
            posting.PaymentIntentId,
            timeProvider.GetUtcNow().DateTime,
            legs);

        await transactionRepository.AddAsync(transaction, ct);
        await CommitPostingAsync(ct);
    }

    private async Task<LedgerAccountEntity> ResolveAccountAsync(
        LedgerAccountRef reference,
        Currency currency,
        Dictionary<LedgerAccountRef, LedgerAccountEntity> resolved,
        CancellationToken ct)
    {
        if (resolved.TryGetValue(reference, out var cached))
            return cached;

        var account = await accountRepository.FindAsync(reference.Type, reference.OwnerId, currency, ct)
            ?? await accountRepository.AddAsync(LedgerAccountEntity.Create(reference.Type, reference.OwnerId, currency), ct);

        resolved[reference] = account;
        return account;
    }

    private async Task CommitPostingAsync(CancellationToken ct)
    {
        while (true)
        {
            try
            {
                await context.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (
                IsDuplicateKey(ex, LedgerTransactionEntityConfiguration.PostingIdentityIndex))
            {
                context.ChangeTracker.Clear();
                return;
            }
            catch (DbUpdateException ex) when (
                IsDuplicateKey(ex, LedgerAccountEntityConfiguration.IdentityIndex))
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

    private static bool IsDuplicateKey(DbUpdateException ex, string indexName) =>
        ex.IsDuplicateKey() &&
        ex.InnerException?.Message.Contains(indexName, StringComparison.Ordinal) == true;
}
