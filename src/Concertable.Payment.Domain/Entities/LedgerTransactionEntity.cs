namespace Concertable.Payment.Domain.Entities;

public sealed class LedgerTransactionEntity : IIdEntity
{
    private readonly List<LedgerEntryEntity> entries = [];

    private LedgerTransactionEntity() { }

    private LedgerTransactionEntity(int bookingId, string? paymentIntentId, DateTime occurredAt)
    {
        BookingId = bookingId;
        PaymentIntentId = paymentIntentId;
        OccurredAt = occurredAt;
    }

    public int Id { get; private set; }
    public int BookingId { get; private set; }
    public string? PaymentIntentId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public IReadOnlyList<LedgerEntryEntity> Entries => entries.AsReadOnly();

    public static LedgerTransactionEntity Post(
        int bookingId,
        string? paymentIntentId,
        DateTime occurredAt,
        IReadOnlyCollection<LedgerLeg> legs)
    {
        if (legs.Count < 2)
            throw new DomainException("A ledger transaction must have at least two entries.");

        var currency = legs.First().Amount.Currency;
        if (legs.Any(leg => leg.Amount.Currency != currency))
            throw new DomainException("All ledger entries in a transaction must share one currency.");

        if (legs.Any(leg => leg.Amount.Amount <= 0))
            throw new DomainException("Each ledger entry amount must be positive.");

        var transaction = new LedgerTransactionEntity(bookingId, paymentIntentId, occurredAt);
        foreach (var leg in legs)
            transaction.entries.Add(new LedgerEntryEntity(leg.Account, leg.Direction, leg.Amount));

        if (transaction.entries.Sum(entry => entry.Amount) != 0)
            throw new DomainException("Ledger transaction does not balance: debits and credits must sum to zero.");

        return transaction;
    }
}
