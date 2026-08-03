namespace Concertable.Payment.Domain.Entities;

internal sealed class LedgerAccountEntity : IIdEntity
{
    private LedgerAccountEntity() { }

    private LedgerAccountEntity(LedgerAccountType type, Guid? ownerId, Currency currency)
    {
        Type = type;
        OwnerId = ownerId;
        Currency = currency;
    }

    public int Id { get; private set; }
    public LedgerAccountType Type { get; private set; }
    public Guid? OwnerId { get; private set; }
    public Currency Currency { get; private set; }

    public static LedgerAccountEntity Create(LedgerAccountType type, Guid? ownerId, Currency currency) =>
        new(type, ownerId, currency);
}
