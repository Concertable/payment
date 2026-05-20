namespace Concertable.Payment.Domain;

public class SettlementTransactionEntity : TransactionEntity
{
    private SettlementTransactionEntity() { }

    private SettlementTransactionEntity(Guid fromUserId, Guid toUserId, string paymentIntentId, long amount, TransactionStatus status, int bookingId)
        : base(fromUserId, toUserId, paymentIntentId, amount, status)
    {
        BookingId = bookingId;
    }

    public override TransactionType TransactionType => TransactionType.Settlement;
    public int BookingId { get; private set; }

    public static SettlementTransactionEntity Create(Guid fromUserId, Guid toUserId, string paymentIntentId, long amount, TransactionStatus status, int bookingId)
        => new(fromUserId, toUserId, paymentIntentId, amount, status, bookingId);
}
