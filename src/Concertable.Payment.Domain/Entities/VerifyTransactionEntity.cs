namespace Concertable.Payment.Domain.Entities;

internal sealed class VerifyTransactionEntity : TransactionEntity
{
    private VerifyTransactionEntity() { }

    private VerifyTransactionEntity(Guid payerId, string paymentIntentId, PaymentOperationReference reference)
        : base(payerId, Guid.Empty, paymentIntentId, 100, TransactionStatus.Complete, reference) { }

    public override TransactionType TransactionType => TransactionType.Verify;
    public static VerifyTransactionEntity Create(
        Guid payerId,
        string paymentIntentId,
        PaymentOperationReference reference) =>
        new(payerId, paymentIntentId, reference);
}
