namespace Concertable.Payment.Infrastructure;

internal sealed class LedgerService : ILedgerService
{
    private readonly ILedgerAccountRepository accountRepository;
    private readonly ILedgerTransactionRepository transactionRepository;
    private readonly TimeProvider timeProvider;

    public LedgerService(
        ILedgerAccountRepository accountRepository,
        ILedgerTransactionRepository transactionRepository,
        TimeProvider timeProvider)
    {
        this.accountRepository = accountRepository;
        this.transactionRepository = transactionRepository;
        this.timeProvider = timeProvider;
    }

    public async Task StageAsync(LedgerPosting posting, CancellationToken ct = default)
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
    }

    private async Task<LedgerAccountEntity> ResolveAccountAsync(
        LedgerAccountRef reference,
        Currency currency,
        Dictionary<LedgerAccountRef, LedgerAccountEntity> resolved,
        CancellationToken ct)
    {
        if (resolved.TryGetValue(reference, out var cached))
            return cached;

        var account = await accountRepository.GetOrCreateAsync(reference.Type, reference.OwnerId, currency, ct);

        resolved[reference] = account;
        return account;
    }
}
