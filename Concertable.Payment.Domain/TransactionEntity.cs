namespace Concertable.Payment.Domain;

public abstract class TransactionEntity : IIdEntity, IAuditable
{
    protected TransactionEntity() { }

    protected TransactionEntity(Guid fromUserId, Guid toUserId, string paymentIntentId, long amount, TransactionStatus status)
    {
        FromUserId = fromUserId;
        ToUserId = toUserId;
        PaymentIntentId = paymentIntentId;
        Amount = amount;
        Status = status;
    }

    public int Id { get; private set; }
    public abstract TransactionType TransactionType { get; }
    public Guid FromUserId { get; private set; }
    public Guid ToUserId { get; private set; }
    public string PaymentIntentId { get; private set; } = null!;
    public long Amount { get; private set; }
    public TransactionStatus Status { get; private set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }

    public void Complete()
    {
        if (Status != TransactionStatus.Pending)
            return;
        Status = TransactionStatus.Complete;
    }

    public void Fail()
    {
        if (Status != TransactionStatus.Pending)
            return;
        Status = TransactionStatus.Failed;
    }
}
