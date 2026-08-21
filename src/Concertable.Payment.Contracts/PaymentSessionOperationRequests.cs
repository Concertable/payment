using Concertable.Kernel.ValueObjects;

namespace Concertable.Payment.Contracts;

public sealed record PaymentSessionOperationRequest(
    Guid OperationId,
    PaymentSessionKind Kind,
    string OperationType,
    string ConsumerCorrelation,
    Guid PayerOwnerId,
    Guid? PayeeOwnerId,
    long? AmountMinor,
    Currency? Currency,
    PaymentSessionFundsRouting FundsRouting);

public sealed record PaymentSessionRetryRequest(
    Guid OperationId,
    Guid ExpectedAttemptId,
    long ExpectedRevision,
    Guid OwnerId);

public sealed record PaymentSessionStatusRequest(
    Guid OperationId,
    Guid OwnerId);
