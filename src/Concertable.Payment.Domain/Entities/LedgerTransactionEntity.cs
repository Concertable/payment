namespace Concertable.Payment.Domain.Entities;

internal sealed class LedgerTransactionEntity : IIdEntity
{
    private readonly List<LedgerEntryEntity> entries = [];

    private LedgerTransactionEntity() { }

    private LedgerTransactionEntity(
        LedgerPostingType postingType,
        string externalId,
        PaymentOperationReference reference,
        string? paymentIntentId,
        DateTime occurredAt)
    {
        PostingType = postingType;
        ExternalId = externalId;
        OperationType = reference.OperationType;
        ClientReference = reference.ClientReference;
        PaymentIntentId = paymentIntentId;
        OccurredAt = occurredAt;
    }

    public int Id { get; private set; }
    public LedgerPostingType PostingType { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public string OperationType { get; private set; } = null!;
    public string ClientReference { get; private set; } = null!;
    public string? PaymentIntentId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public IReadOnlyList<LedgerEntryEntity> Entries => entries.AsReadOnly();

    public static LedgerTransactionEntity Post(
        LedgerPostingType postingType,
        string externalId,
        PaymentOperationReference reference,
        string? paymentIntentId,
        DateTime occurredAt,
        IReadOnlyCollection<LedgerLeg> legs)
    {
        ValidatePosting(externalId, legs.Select(leg => (leg.Direction, leg.Amount)));
        reference = reference.EnsureValid();

        var transaction = new LedgerTransactionEntity(postingType, externalId, reference, paymentIntentId, occurredAt);
        foreach (var leg in legs)
            transaction.entries.Add(new LedgerEntryEntity(leg.Account, leg.Direction, leg.Amount));

        return transaction;
    }

    /// <summary>The <see cref="Post"/> balance rules, callable before any account is eagerly upserted.</summary>
    public static void ValidatePosting(string externalId, IEnumerable<(LedgerDirection Direction, Money Amount)> legs)
    {
        var legList = legs as IReadOnlyCollection<(LedgerDirection Direction, Money Amount)> ?? legs.ToList();

        if (string.IsNullOrWhiteSpace(externalId))
            throw new DomainException("A ledger transaction must identify its external financial event.");

        if (legList.Count < 2)
            throw new DomainException("A ledger transaction must have at least two entries.");

        var currency = legList.First().Amount.Currency;
        if (legList.Any(leg => leg.Amount.Currency != currency))
            throw new DomainException("All ledger entries in a transaction must share one currency.");

        if (legList.Any(leg => leg.Amount.Amount <= 0))
            throw new DomainException("Each ledger entry amount must be positive.");

        var signedSum = legList.Sum(leg =>
            leg.Direction == LedgerDirection.Debit ? leg.Amount.ToMinorUnits() : -leg.Amount.ToMinorUnits());
        if (signedSum != 0)
            throw new DomainException("Ledger transaction does not balance: debits and credits must sum to zero.");
    }
}
