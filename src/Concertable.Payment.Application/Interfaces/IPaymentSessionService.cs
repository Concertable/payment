using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Domain;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentSessionService
{
    Task<Result<PaymentSessionExecution, PaymentOperationError>> CreateOrReplayAsync(
        PaymentSessionSpecification specification,
        CancellationToken ct = default);

    Task<Result<PaymentSessionExecution, PaymentOperationError>> RetryAsync(
        Guid operationId,
        Guid expectedAttemptId,
        long expectedRevision,
        CancellationToken ct = default);

    Task<Result<PaymentSessionStatus, PaymentOperationError>> RefreshAsync(
        Guid operationId,
        CancellationToken ct = default);
}
