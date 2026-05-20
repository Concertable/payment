namespace Concertable.Payment.Domain;

public class TicketTransactionEntity : TransactionEntity
{
    private TicketTransactionEntity() { }

    private TicketTransactionEntity(Guid fromUserId, Guid toUserId, string paymentIntentId, long amount, TransactionStatus status, int concertId)
        : base(fromUserId, toUserId, paymentIntentId, amount, status)
    {
        ConcertId = concertId;
    }

    public override TransactionType TransactionType => TransactionType.Ticket;
    public int ConcertId { get; private set; }

    public static TicketTransactionEntity Create(Guid fromUserId, Guid toUserId, string paymentIntentId, long amount, TransactionStatus status, int concertId)
        => new(fromUserId, toUserId, paymentIntentId, amount, status, concertId);
}
