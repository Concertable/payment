namespace Concertable.Payment.Domain.Entities;

public sealed class SettlementTransactionEntity : TransactionEntity
{
    private SettlementTransactionEntity() { }

    private SettlementTransactionEntity(Guid payerId, Guid payeeId, string paymentIntentId, long amount, TransactionStatus status, int bookingId)
        : base(payerId, payeeId, paymentIntentId, amount, status)
    {
        BookingId = bookingId;
    }

    public override TransactionType TransactionType => TransactionType.Settlement;
    public int BookingId { get; private set; }

    public static SettlementTransactionEntity Create(Guid payerId, Guid payeeId, string paymentIntentId, long amount, TransactionStatus status, int bookingId)
        => new(payerId, payeeId, paymentIntentId, amount, status, bookingId);
}
