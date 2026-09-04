using Reunion;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.Client;

public interface IEscrowOperationsClient
{
    Task<Result<EscrowDeposit, EscrowDepositError>> DepositAsync(
        Guid operationId,
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowDepositError>> DepositBoundCommissionAsync(
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money gross,
        PaymentOperationReference paymentMethod,
        PaymentSession session,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureAsync(
        Guid operationId,
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money amount,
        PaymentOperationReference authorization,
        CancellationToken ct = default);

    Task<Result<EscrowDeposit, EscrowCaptureError>> CaptureBoundCommissionAsync(
        PaymentOperationReference reference,
        Guid payerId,
        Guid payeeId,
        Money gross,
        PaymentOperationReference authorization,
        Guid commissionBindingId,
        string externalReference,
        CancellationToken ct = default);

    Task<Result<Option<Transfer>, EscrowReleaseOperationError>> ReleaseAsync(
        Guid operationId,
        PaymentOperationReference reference,
        CancellationToken ct = default);

    Task<Result<Option<Refund>, EscrowRefundError>> RefundAsync(
        Guid operationId,
        PaymentOperationReference reference,
        CancellationToken ct = default);

    Task<Result<Option<Refund>, EscrowRefundError>> RefundBoundCommissionAsync(
        PaymentOperationReference reference,
        Money gross,
        CancellationToken ct = default);
}
