namespace Concertable.Payment.Domain.Entities;

internal sealed class LedgerEntryEntity : IIdEntity
{
    private LedgerEntryEntity() { }

    internal LedgerEntryEntity(LedgerAccountEntity account, LedgerDirection direction, Money amount)
    {
        Account = account;
        Direction = direction;
        Amount = direction == LedgerDirection.Debit ? amount.ToMinorUnits() : -amount.ToMinorUnits();
        Currency = amount.Currency;
    }

    public int Id { get; private set; }
    public int LedgerTransactionId { get; private set; }
    public int LedgerAccountId { get; private set; }
    public LedgerAccountEntity Account { get; private set; } = null!;
    public LedgerDirection Direction { get; private set; }
    public long Amount { get; private set; }
    public Currency Currency { get; private set; }
}
