namespace Concertable.Payment.Domain.Entities;

public sealed class TicketTransactionEntity : TransactionEntity
{
    private TicketTransactionEntity() { }

    private TicketTransactionEntity(Guid payerId, Guid payeeId, string paymentIntentId, long amount, TransactionStatus status, int concertId)
        : base(payerId, payeeId, paymentIntentId, amount, status)
    {
        ConcertId = concertId;
    }

    public override TransactionType TransactionType => TransactionType.Ticket;
    public int ConcertId { get; private set; }

    public static TicketTransactionEntity Create(Guid payerId, Guid payeeId, string paymentIntentId, long amount, TransactionStatus status, int concertId)
        => new(payerId, payeeId, paymentIntentId, amount, status, concertId);
}
