using Concertable.Kernel;

namespace Concertable.Payment.Domain.Entities;

internal abstract class TransactionEntity : IIdEntity, IAuditable
{
    protected TransactionEntity() { }

    protected TransactionEntity(
        Guid payerId,
        Guid payeeId,
        string paymentIntentId,
        long amount,
        TransactionStatus status,
        PaymentOperationReference reference)
    {
        reference = reference.EnsureValid();

        PayerId = payerId;
        PayeeId = payeeId;
        PaymentIntentId = paymentIntentId;
        Amount = amount;
        Status = status;
        OperationType = reference.OperationType;
        ClientReference = reference.ClientReference;
    }

    public int Id { get; private set; }
    public abstract TransactionType TransactionType { get; }
    public Guid PayerId { get; private set; }
    public Guid PayeeId { get; private set; }
    public string PaymentIntentId { get; private set; } = null!;
    public long Amount { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string OperationType { get; private set; } = null!;
    public string ClientReference { get; private set; } = null!;
    public DateTime? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }

    public UnitResult<TransactionTransitionError> Complete(DateTime completedAt)
    {
        if (Status != TransactionStatus.Pending)
            return UnitResult.Failure<TransactionTransitionError>(new TransactionTransitionError.NotPending(Status));

        Status = TransactionStatus.Complete;
        CompletedAt = completedAt;
        return UnitResult.Success<TransactionTransitionError>();
    }

    public UnitResult<TransactionTransitionError> Fail()
    {
        if (Status != TransactionStatus.Pending)
            return UnitResult.Failure<TransactionTransitionError>(new TransactionTransitionError.NotPending(Status));

        Status = TransactionStatus.Failed;
        return UnitResult.Success<TransactionTransitionError>();
    }
}
