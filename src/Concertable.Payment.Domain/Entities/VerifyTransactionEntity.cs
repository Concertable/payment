namespace Concertable.Payment.Domain.Entities;

public sealed class VerifyTransactionEntity : TransactionEntity
{
    private VerifyTransactionEntity() { }

    private VerifyTransactionEntity(Guid payerId, string paymentIntentId, int applicationId)
        : base(payerId, Guid.Empty, paymentIntentId, 100, TransactionStatus.Complete)
    {
        ApplicationId = applicationId;
    }

    public override TransactionType TransactionType => TransactionType.Verify;
    public int ApplicationId { get; private set; }

    public static VerifyTransactionEntity Create(Guid payerId, string paymentIntentId, int applicationId)
        => new(payerId, paymentIntentId, applicationId);
}
