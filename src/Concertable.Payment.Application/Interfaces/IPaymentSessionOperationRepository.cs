using Concertable.Payment.Domain;

namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentSessionOperationRepository
{
    Task<PaymentSessionOperationEntity?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken ct = default);

    Task<PaymentSessionOperationEntity?> GetByProviderObjectAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default);

    Task<PaymentSessionReservation> ReserveInitialAsync(
        PaymentSessionSpecification specification,
        DateTimeOffset createdAt,
        CancellationToken ct = default);

    Task<PaymentSessionReservation> ReserveNextAttemptAsync(
        Guid operationId,
        Guid expectedAttemptId,
        long expectedRevision,
        DateTimeOffset createdAt,
        CancellationToken ct = default);
}
