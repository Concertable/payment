namespace Concertable.Payment.Domain;

public class VerifyTransactionEntity : TransactionEntity
{
    private VerifyTransactionEntity() { }

    private VerifyTransactionEntity(Guid fromUserId, string paymentIntentId, int applicationId)
        : base(fromUserId, Guid.Empty, paymentIntentId, 100, TransactionStatus.Complete)
    {
        ApplicationId = applicationId;
    }

    public override TransactionType TransactionType => TransactionType.Verify;
    public int ApplicationId { get; private set; }

    public static VerifyTransactionEntity Create(Guid fromUserId, string paymentIntentId, int applicationId)
        => new(fromUserId, paymentIntentId, applicationId);
}
