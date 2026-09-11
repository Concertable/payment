namespace Concertable.Payment.Domain.Entities;

internal sealed class PaymentTransactionEntity : TransactionEntity
{
    private PaymentTransactionEntity() { }

    private PaymentTransactionEntity(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        long amount,
        TransactionStatus status,
        PaymentOperationReference reference)
        : base(payerId, payeeId, paymentIntentId, amount, status, reference) { }

    public override TransactionType TransactionType => TransactionType.Payment;

    public static PaymentTransactionEntity Create(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        long amount,
        TransactionStatus status,
        PaymentOperationReference reference) =>
        new(payerId, payeeId, paymentIntentId, amount, status, reference);
}
